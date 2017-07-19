namespace Fable.Websockets.Elmish

open System
open Fable.Core
open Fable.Websockets.Protocol
open FSharp.Collections
open Fable.Websockets.Client
open Fable.Websockets.Protocol


type WebsocketMsg<'serverMsg,'clientMsg> = SocketHandle<'serverMsg,'clientMsg> * WebsocketEvent<'clientMsg>

module Types =
    type SocketHandle<'serverMsg,'clientMsg> (sink: 'serverMsg -> unit, 
                                              source: IObservable<WebsocketEvent<'clientMsg>>, 
                                              closeHandle: ClosedCode -> string -> unit) =             
        member val internal ConnectionId = System.Guid.NewGuid()
        member val internal CloseHandle = closeHandle
        member val internal Sink = sink
        member val internal Source = source    
        member val internal Subscription:IDisposable option = None with get, set
        
        override x.GetHashCode() =
            x.ConnectionId.GetHashCode()

        override x.Equals(b) =
            match b with
            | :? SocketHandle<'serverMsg,'clientMsg> as c -> x.ConnectionId = c.ConnectionId
            | _ -> false
    type Msg<'serverMsg, 'clientMsg, 'applicationMsg> =
            | WebsocketMsg of SocketHandle<'serverMsg,'clientMsg> * WebsocketEvent<'clientMsg>
            | ApplicationMsg of 'applicationMsg

module SocketHandle =        
    open Types
    let Blackhole () : SocketHandle<'serverMsg,'clientMsg> = 
        SocketHandle<'serverMsg,'clientMsg>(ignore, Fable.Websockets.Observables.Subject(),fun _ _ -> ())

    [<PassGenerics>]
    let Create address (dispatcher: Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) =         
        let (sink,source, closeHandle) = establishWebsocketConnection<'serverMsg,'clientMsg> address                    
        let connection = SocketHandle<'serverMsg, 'clientMsg> (sink,source,closeHandle)
        
        let subscription = source 
                           |> Observable.subscribe (fun msg -> Msg.WebsocketMsg (connection,msg) |> dispatcher)                                
                           |> Some

        connection.Subscription <- subscription   
                      
module Cmd =
    open System
    open FSharp.Collections

    open Fable.Websockets.Client
    open Fable.Websockets.Protocol
    open Types
    
    
    [<PassGenerics>]    
    let public ofSocketMessage (socket: SocketHandle<'serverMsg,'clientMsg>) (message:'serverMsg) =             
        [fun _ -> socket.Sink message]
    
    [<PassGenerics>]
    let public tryOpenSocket address =            
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> SocketHandle.Create address dispatcher]
    [<PassGenerics>]    
    let public closeSocket (socket: SocketHandle<'serverMsg,'clientMsg>) code reason =            
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> do socket.CloseHandle code reason] 


module Program =
    let withSockets (socketUpdate :  WebsocketMsg<'serverMsg,'clientMsg>,-> 'model -> ('model * Cmd<'msg>))  (program : Program<'a,'model,'msg,'view>) =           
        0