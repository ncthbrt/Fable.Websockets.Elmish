module FileBrowser.Server.FileHelpers

open System
open System.IO
open FileBrowser.Protocol

/// Custom operator for combining paths
let ( +/ ) path1 path2 = Path.Combine(path1, path2)
    


let getDirectoryListing directory = 
         
     let directories = Directory.GetDirectories directory |> Seq.map (Path.GetFileName >> FileReference.Folder)     
     let files = Directory.GetFiles directory |> Seq.map (Path.GetFileName>>FileReference.File)
     
     directories |> Seq.append files |> Seq.toList

// The following code was ported from 
// https://stackoverflow.com/questions/5617320/given-full-path-check-if-path-is-subdirectory-of-some-other-path-or-otherwise/31941159#31941159
// and is designed to test whether some path is a sub path of another

let private right (str:string) length = 
    if isNull str then 
        failwith "String cannot be null"
    elif length <  0 then
        failwith "Length cannot be less than zero"
    elif (length < str.Length) then
        str.Substring (str.Length - length) 
    else 
        str
         
let private withEnding (ending:string) (str:string) = 
    if isNull str then
        ending
    else 
        let possibleResult = seq { 0 .. ending.Length } |> Seq.tryFind (fun i -> (str + (right ending i)).EndsWith ending)
     
        match possibleResult with
        | Some i -> str + (right ending i)
        | None -> str

let normalize (path:string) = Path.GetFullPath (path.Replace('/', '\\') |> withEnding "\\")

let isChildPathOf (baseDirPath:string) (targetPath:string) =

    let normalizedPath = normalize targetPath
    let normalizedBaseDirPath = normalize baseDirPath
    
    normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase)
    
let initialDirectory = "./wwwroot" 