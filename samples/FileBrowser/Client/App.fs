module FileBrowser.Client

open System

open FileBrowser.Protocol

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import

open Elmish
open Elmish.React

open Fable.Websockets.Client
open Fable.Websockets.Protocol

open Fable.Helpers.React.Props
module R = Fable.Helpers.React

open Fable.Websockets.Elmish
open Fable.Websockets.Elmish.Types

type ViewModel = 
    | EmailPrompt of string    
    | Folder of (string*(FileReference list))
    | File of string*string
    | NoViewModel  

type ConnectionState = NotConnected | Connected    

type Model = { viewModel: ViewModel; 
               connectionState: ConnectionState; 
               socket: SocketHandle<ServerMsg,ClientMsg>
               email: string
               initialDirectory: string option
             }

type ApplicationMsg = 
    | SetEmailText of string
    | SubmitUserEmail of string    
    | OpenChildFolder of string
    | OpenFile of string
    | OpenParentFolder
    | CloseFile

type MsgType = Msg<ServerMsg,ClientMsg,ApplicationMsg>

let inline initialState () =  
    ({ connectionState = NotConnected
       viewModel = EmailPrompt ""
       email="" 
       socket = SocketHandle.Blackhole()      
       initialDirectory = None  
     }, Cmd.none)

let inline socketMsgUpdate (msg:ClientMsg) prevState = 
    match msg with    
    | ClientMsg.Challenge -> prevState, Cmd.ofSocketMessage prevState.socket (Greet {email=prevState.email})
    | Welcome -> prevState, Cmd.ofSocketMessage prevState.socket ListCurrentDirectory
    | DirectoryListing ((name,listing) as d) ->
        let initialDirectory =
            match prevState.initialDirectory with 
            | None -> Some name
            | (Some _) as id -> id
        ({ prevState with viewModel = Folder d; initialDirectory = initialDirectory  }, Cmd.none)
    | NotFound fileRef -> prevState, Cmd.none
    | DirectoryChanged fileRef -> prevState, Cmd.ofSocketMessage prevState.socket ListCurrentDirectory
    | FileContents contents -> { prevState with viewModel = File (contents.name,contents.contents)  }, Cmd.none    

let inline applicationMsgUpdate (msg: ApplicationMsg) prevState =
    match msg with
    | SubmitUserEmail email -> ({ prevState with viewModel = NoViewModel }, Cmd.tryOpenSocket "ws://localhost:8083/websocket")
    | SetEmailText email -> ({ prevState with viewModel = EmailPrompt email; email = email }, Cmd.none)
    | OpenChildFolder folder -> prevState, Cmd.ofSocketMessage prevState.socket (MoveToSubdirectory folder)
    | OpenFile file -> prevState, Cmd.ofSocketMessage prevState.socket (GetFileContents file)    
    | CloseFile -> prevState, Cmd.ofSocketMessage prevState.socket ListCurrentDirectory
    | OpenParentFolder -> prevState, Cmd.ofSocketMessage prevState.socket MoveToParentDirectory

let inline update msg prevState = 
    match msg with
    | ApplicationMsg amsg -> applicationMsgUpdate amsg prevState
    | WebsocketMsg (socket, Opened) -> ({ prevState with socket = socket; connectionState = Connected }, Cmd.none)    
    | WebsocketMsg (_, Msg socketMsg) -> (socketMsgUpdate socketMsg prevState)
    | _ -> (prevState, Cmd.none)

let emailView (email:string) dispatch =        
    R.div[] [
        R.h1 [] [R.str "Enter your email"]
        R.br []
        R.input [Value email; OnChange (fun e-> (dispatch<<ApplicationMsg<<SetEmailText<<string) e.target?value)]
        R.input [Type "submit"; OnClick (fun e-> (dispatch<<ApplicationMsg<<SubmitUserEmail<<string) email)]
    ]

let fileView name contents dispatch = 
    R.div [] 
          [
             R.a [Href "#" ;OnClick (fun _ -> dispatch (ApplicationMsg CloseFile))] [R.str <| "👈" + name]
             R.br []
             R.text [] [R.str contents]
          ]

let fileEntrySubview dispatch fileReference =
    match fileReference with
    | FileReference.File file -> [R.a [Href "#";OnClick (fun _ -> (dispatch<<ApplicationMsg<<OpenFile) file)] [R.str ("📝" + file)]; R.br []]
    | FileReference.Folder folder -> [R.a [Href "#"; OnClick (fun _ -> (dispatch<<ApplicationMsg<<OpenChildFolder) folder)] [R.str ("📁" + folder)]; R.br []]    


let folderView intialDirectory (folder: string, files: FileReference list) dispatch =        
    
    let headers = if intialDirectory = folder then
                    [R.div [] [R.str folder]; R.br []]                     
                  else
                    [R.a [Href "#"; OnClick (fun _ -> dispatch (ApplicationMsg OpenParentFolder))] [R.str <| "☝️ "+folder];R.br []]                     

    let files = files 
                |> List.fold (fun prev fileReference -> (fileEntrySubview dispatch fileReference) @ prev) [] 
                |> List.rev

    R.div [] (headers @ files)
        

let view model dispatch = 
    let loader = R.text [] [R.str "loading..."]    

    match model.viewModel with
    | EmailPrompt email -> emailView email dispatch 
    | Folder f -> folderView (model.initialDirectory |> Option.get) f  dispatch
    | File (name, contents) -> fileView name contents dispatch
    | NoViewModel -> loader
    

Program.mkProgram initialState update view
|> Program.withReact "elmish-app"
|> Program.withConsoleTrace
|> Program.run
