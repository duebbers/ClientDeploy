open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
let converter = Fable.JsonConverter()

open ClientDeploy
open ClientDeploy.Manifests
open ClientDeploy.RepoParser
open ClientDeploy.Resources

let private mkChannelUrl absolutebase ch = sprintf "%s/channel/%s" absolutebase ch
let private mkVersionUrl absolutebase ch v = sprintf "%s/channel/%s/version/%s" absolutebase ch v
let private mkResourceUrl absolutebase ch v res = sprintf "%s/channel/%s/version/%s/resources/%s" absolutebase ch v res
let private mkGlobalResourceUrl absolutebase ch res = sprintf "%s/channel/%s/resources/%s" absolutebase ch res

let private versionSummary absolutebase ch v =
  { VersionSummary.Version = v.Config.SemVer.ToString()
    VersionDetailsUrl = mkVersionUrl absolutebase ch v.Config.Version
    ReleaseNotes = v.Config.ReleaseNotes
    Hash = v.Hash
    Deprecated = v.Config.Deprecated }

let private versionDetails absolutebase ch (v:Version) =
  let expectations =
    v.Manifest.Expectations
    |> List.map (function
        | FileExpected (target,{ Resource.Source = path,source ; Hash = hash ; Size = size }) ->
          if (path="~")
            then FileExpected (target,{ Resource.Source = (mkResourceUrl absolutebase ch v.Config.Version source),"" ; Hash = hash ; Size = size })
            else FileExpected (target,{ Resource.Source = (mkGlobalResourceUrl absolutebase ch path),source ; Hash = hash ; Size = size })
        | ExactFileExpected (target,fhash,{ Resource.Source = path,source ; Hash = hash ; Size = size }) ->
          if (path="~")
            then ExactFileExpected (target,fhash,{ Resource.Source = (mkResourceUrl absolutebase ch v.Config.Version source),"" ; Hash = hash ; Size = size })
            else ExactFileExpected (target,fhash,{ Resource.Source = (mkGlobalResourceUrl absolutebase ch path),source ; Hash = hash ; Size = size })
        | x -> x)

  { VersionDetails.Version = v.Config.SemVer.ToString()
    ReleaseNotes = v.Config.ReleaseNotes
    Hash = v.Hash
    Deprecated = v.Config.Deprecated
    Manifest = { v.Manifest with Expectations = expectations } }

let private channelversion absolutebase repo ch v =
  maybe {
    let! channel = repo.Channels |> Map.tryFind ch
    let! version = channel.Versions |> Map.tryFind v
    return versionDetails absolutebase ch version
  }

let private channellatest absolutebase repo ch =
  maybe {
    let! channel = repo.Channels |> Map.tryFind ch
    let! latest = channel.Latest
    return versionDetails absolutebase ch latest
  }

let channel absolutebase (repo:Repository) (ch:string) =
  repo.Channels
  |> Map.tryFind ch
  |> Option.map (fun ch ->
      let versions =
        ch.Versions
        |> Map.toSeq
        |> Seq.sortByDescending (fun (_,v) -> v.Config.SemVer)
        |> Seq.map (snd>>(versionSummary absolutebase ch.Name))
        |> Seq.toList
      { ChannelDetails.Channel = ch.Name
        DisplayName = ch.Config.Displayname
        Description = ch.Config.Description
        LatestVersionUrl = ch.Latest |> Option.map (fun _ -> sprintf "%s/%s" (mkChannelUrl absolutebase ch.Name) "latest")
        Versions = versions } )

let channels absolutebase (repo:Repository) =
  repo.Channels
  |> Map.toSeq
  |> Seq.map (fun (_,ch) ->
      { ChannelSummary.Channel = ch.Name
        DisplayName = ch.Config.Displayname
        ChannelDetailsUrl = mkChannelUrl absolutebase ch.Name
        Description = ch.Config.Description })
  |> Seq.sortBy (fun l -> l.Channel)
  |> Seq.toList


let jsonContent = Writers.setHeader "Content-Type" "application/json;charset=utf-8"
let jsonResponse data = jsonContent >=> OK (Newtonsoft.Json.JsonConvert.SerializeObject(data,converter))
let jsonResponseOpt =
  function
  | Some data -> jsonContent >=> OK (Newtonsoft.Json.JsonConvert.SerializeObject(data,converter))
  | None -> RequestErrors.NOT_FOUND "Sorry, the requested resource was not found."


[<EntryPoint>]
let main argv =

  if (Array.length argv<>2) then failwith "usage: <executable> http://ipaddr:port path_to_repo"

  let absolutebase = argv.[0]
  let parts = absolutebase.Split(":")
  let path_to_repo = argv.[1]
  let repository = scan path_to_repo

  let protocol = parts.[0]
  let ipaddress = parts.[1].Trim([|'/'|])
  let port = System.Int32.Parse (parts.[2])
  if (protocol<>"http") then failwithf "Protocol %s not supported" protocol


  let app =
    choose
      [ GET >=> path "/" >=> OK "<h1>ClientDeploy Repository</h1><a href='/repo'>Repository Description</a>"
        GET >=> path "/repo" >=>  jsonResponse ({ RepositoryDescription.APIv1=(sprintf "%s/apiv1" absolutebase) ; CanonicalBaseUrl = absolutebase })
        GET >=> path "/apiv1" >=>  jsonResponse ({ RepositoryAPIv1.UpdaterUrl=null ; ChannelListUrl=(sprintf "%s/channels" absolutebase) })
        GET >=> path "/channels" >=>  jsonResponse (channels absolutebase repository)
        GET >=> pathScan "/channel/%s/latest" (fun (ch) -> jsonResponseOpt (channellatest absolutebase repository ch) )
        GET >=> pathScan "/channel/%s/version/%s" (fun (ch,v) -> jsonResponseOpt (channelversion absolutebase repository ch v) )
        GET >=> pathScan "/channel/%s" (fun ch -> jsonResponseOpt (channel absolutebase repository ch) ) ]
  startWebServer { defaultConfig with bindings = [ HttpBinding.createSimple Protocol.HTTP ipaddress port ] } app
  System.Console.ReadLine() |> ignore
  0