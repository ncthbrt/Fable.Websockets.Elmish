module FileBrowser.Server.App


open System
open System.IO

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open Fable.Websockets.Suave
open Fable.Websockets.Observables
open Fable.Websockets.Protocol

open FileBrowser.Protocol
open FileBrowser.Server.FileHelpers

type ServerState ={ currentDirectory:string; user: FileBrowser.Protocol.User option }

type Event<'serverProtocol>  =
    | WebsocketEvent of WebsocketEvent<'serverProtocol>        
    | DirectoryOpened of string

type Effect<'clientProtocol> =     
    | Send of 'clientProtocol    
    | OpenDirectory of string
    | ListCurrentDirectory
    | OpenFile of string    
    | AccessViolation
    | NoEffect    

let authenticationGuard prevState effect = 
    if Option.isSome prevState.user then 
        effect
    else
        AccessViolation

let fileGuard prevState =     
    let isAccessViolation d =
        let path = (prevState.currentDirectory +/ d)
        not (path |> isChildPathOf (Directory.GetCurrentDirectory() +/ initialDirectory))

    function
        | (OpenDirectory d) as effect -> if isAccessViolation d then AccessViolation else effect            
        | (OpenFile d) as effect -> if isAccessViolation d then AccessViolation else effect            
        | effect -> effect

let reduceSocketMessage prevState: ServerMsg -> (ServerState*Effect<ClientMsg>) =
    let authenticationGuard = authenticationGuard prevState
    let fileGuard = fileGuard prevState

    let openDirectory = fileGuard << authenticationGuard << OpenDirectory
    let openFile = fileGuard << authenticationGuard << Effect.OpenFile

    let listCurrentDirectory = authenticationGuard Effect.ListCurrentDirectory           

    function
    | Greet user -> ({ prevState with user = Some user }, Send Welcome)                     
    | ServerMsg.ListCurrentDirectory -> (prevState, listCurrentDirectory)
    | MoveToSubdirectory dir -> (prevState, openDirectory dir)
    | MoveToParentDirectory -> (prevState, openDirectory "../")
    | GetFileContents file -> (prevState, openFile file)

let reducer (prevState, _) =
    function
    | WebsocketEvent (Msg msg) -> reduceSocketMessage prevState msg
    | WebsocketEvent Opened -> prevState, Send Challenge
    | DirectoryOpened dir -> {prevState with currentDirectory=dir}, NoEffect
    | _ -> prevState, NoEffect


let effects socketEventSink dispatcher closeHandle = function
              | _, Send msg -> msg |> socketEventSink              
              | state, OpenDirectory dir -> 
                let newDirectory = state.currentDirectory +/ dir                     

                if Directory.Exists newDirectory  then 
                    // Dispatch action to set current directory
                    DirectoryOpened newDirectory |> dispatcher
                    // Send directory listing to client
                    getDirectoryListing newDirectory  |> fun listing -> ClientMsg.DirectoryChanged (newDirectory |> normalize ,listing) |> socketEventSink                        
                else 
                    // Notify client that file doesn't exist
                    ClientMsg.NotFound (FileReference.Folder newDirectory) |> socketEventSink

              | state, ListCurrentDirectory -> getDirectoryListing state.currentDirectory |> fun listing -> DirectoryListing (state.currentDirectory |> normalize, listing) |> socketEventSink 
              | state, OpenFile file -> 
                    let path = state.currentDirectory +/ file
                    if File.Exists path then 
                        let bytes = File.ReadAllBytes path 
                        let fileContents = FileContents { name = path; contents = bytes |> System.Text.Encoding.UTF8.GetString }
                        fileContents |> socketEventSink
                    else 
                        NotFound (File path) |> socketEventSink                            
              | _, AccessViolation -> 
                    (closeHandle Fable.Websockets.Protocol.PolicyViolation "ACCESS VIOLATION")                                                
              | _, NoEffect -> ()

let onConnectionEstablished closeHandle socketEventSource socketEventSink = 

    let initialState = { currentDirectory = initialDirectory; user = None }        
    
    // Event Sources
    let socketEventSource = socketEventSource |> Observable.map WebsocketEvent // Feed of socket events 
    let recursiveActionSource = Subject() // This source is used when effectors need to dispatch futher events    
    let combinedSources = Observable.merge socketEventSource recursiveActionSource        

    // Reducer
    let reducer = combinedSources |> Observable.scan reducer (initialState, NoEffect)    

    // Effectors  
    let dispatcher = recursiveActionSource.Next
    let effects = effects socketEventSink dispatcher closeHandle
  
    
    // Subscribe to effect stream
    // Subscription is returned to prevent 
    // resource disposal
    reducer |> Observable.subscribe effects    

let app : WebPart = 
  choose [
    path "/websocket" >=> websocket<ServerMsg,ClientMsg> onConnectionEstablished        
    NOT_FOUND "Found no handlers." 
  ]


[<EntryPoint>]
let main _ =
    if not <| Directory.Exists initialDirectory then 
        failwith "the wwwroot folder not present"        
    else    
        startWebServer { defaultConfig with 
                            logger = Targets.create Verbose [||];       
                            bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8083 ]
                       } app
        0