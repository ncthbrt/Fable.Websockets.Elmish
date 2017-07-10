// include Fake libs
#r "./packages/FAKE/tools/FakeLib.dll"

open Fake
open Fake.DotNetCli
open Fake.ProcessHelper
open Fake.FileSystem
open Fake.YarnHelper


// Directories
let buildDir  = "./build/"
let deployDir = "./deploy/"


// Filesets
let appReferences  =
    !! "/**/*.csproj"
    ++ "/**/*.fsproj"    


let projectFolders  =  [ (filesInDirMatchingRecursive "*.fsproj" (directoryInfo "./src"))  
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
   ["./";"./samples/HelloWorld/Client/"; "./src/Fable.Websockets.Client/"]
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
    [ async { return (DotNetCli.RunCommand (fun p -> {p with WorkingDir = "./samples/HelloWorld/Server/"}) "watch run") }
      async { return (DotNetCli.RunCommand (fun p -> {p with WorkingDir = "./samples/HelloWorld/Client/"}) "fable yarn-run start-sample") }
    ] |> Async.Parallel |> Async.RunSynchronously |> ignore
)


// Build order
"Clean" ==> "Restore" ==> "YarnRestore" ==> "Build"

"Build" ==> "RunElmishSample"

// start build
RunTargetOrDefault "Build"
