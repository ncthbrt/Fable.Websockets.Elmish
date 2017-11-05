// include Fake libs
#r "./packages/build/FAKE/tools/FakeLib.dll"

open Fake
open Fake.DotNetCli
open Fake.ProcessHelper
open Fake.FileSystem
open Fake.YarnHelper


// Directories
let buildDir  = "./build/"
let deployDir = "./deploy/"

let nugetProjectFolders = [ (filesInDirMatchingRecursive "*.fsproj" (directoryInfo "./src"))  
                            (filesInDirMatchingRecursive "*.csproj" (directoryInfo "./src"))
                          ]
                          |> Array.concat
                          |> Seq.map (fun m -> m.Directory.FullName)

let projectFolders  =  [
                         (filesInDirMatchingRecursive "*.fsproj" (directoryInfo "./src"))  
                         (filesInDirMatchingRecursive "*.csproj" (directoryInfo "./src"))
                         (filesInDirMatchingRecursive "*.csproj" (directoryInfo "./samples/"))
                         (filesInDirMatchingRecursive "*.fsproj" (directoryInfo "./samples/"))
                       ]                       
                       |> Array.concat
                       |> Seq.map (fun m -> m.Directory.FullName)



// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; deployDir]
)

Target "YarnRestore" (fun _->        
   ["./";"./samples/FileBrowser/Client/"; "./src/Fable.Websockets.Elmish/"]
   |> Seq.iter (fun dir -> Yarn (fun p ->{ p with Command = Install Standard; WorkingDirectory = dir}))
   |> ignore   
)

Target "Restore" (fun _->    
    projectFolders
    |> Seq.map (fun project-> DotNetCli.Restore (fun p-> {p with Project=project}))
    |> Seq.toArray    
    |> ignore
)

Target "Build" (fun _ ->
    projectFolders
    |> Seq.map (fun project-> DotNetCli.Build (fun p-> { p with Project=project }))
    |> Seq.toArray    
    |> ignore        
)

Target "RunElmishSample" (fun _ ->
    // Start client
    [ async { return (DotNetCli.RunCommand (fun p -> {p with WorkingDir = "./samples/FileBrowser/Server/"}) "watch run") }
      async { return (DotNetCli.RunCommand (fun p -> {p with WorkingDir = "./samples/FileBrowser/Client/"}) "fable webpack-dev-server") }
    ] |> Async.Parallel |> Async.RunSynchronously |> ignore
)

let release =  ReadFile "RELEASE_NOTES.md" |> ReleaseNotesHelper.parseReleaseNotes                

Target "Meta" (fun _ ->
    [ "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
      "<PropertyGroup>"
      "<Description>Library for strongly typed websocket use in Fable</Description>"
      "<PackageProjectUrl>https://github.com/ncthbrt/Fable.Websockets.Elmish</PackageProjectUrl>"
      "<PackageLicenseUrl>https://github.com/ncthbrt/Fable.Websockets/blob/master/LICENSE.md</PackageLicenseUrl>"
      "<PackageIconUrl></PackageIconUrl>"
      "<RepositoryUrl>https://github.com/ncthbrt/Fable.Websockets.Elmish</RepositoryUrl>"
      "<PackageTags>fable;fsharp;elmish;websockets;observables</PackageTags>"
      "<Authors>Nick Cuthbert</Authors>"
      sprintf "<Version>%s</Version>" (string release.SemVer)
      "</PropertyGroup>"
      "</Project>"]
    |> WriteToFile false "src/Meta.props"    
)


Target "Package" (fun _ ->            
    !! @"./src/**/*.fsproj"
    |> Seq.iter (fun project-> DotNetCli.Pack (fun p-> { p with Project = project; OutputPath = currentDirectory+"/build" }))
)

// Build order
"Meta" ==> "Clean" ==> "Restore" ==> "YarnRestore" ==> "Build" ==> "Package"

"Build" ==> "RunElmishSample"

// start build
RunTargetOrDefault "Build"
