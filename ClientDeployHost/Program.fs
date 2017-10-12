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
let private mkResourceUrl absolutebase ch v (res:string) = sprintf "%s/channel/%s/version/%s/resource/%s.dat" absolutebase ch v (res.Replace("-",""))
let private mkGlobalResourceUrl absolutebase ch res = sprintf "%s/channel/%s/resourcepack/%s" absolutebase ch res

let private versionSummary absolutebase ch v =
  { VersionSummary.Version = v.Configuration.SemVer.ToString()
    VersionDetailsUrl = mkVersionUrl absolutebase ch v.Configuration.Version
    ReleaseNotes = v.Configuration.ReleaseNotes
    Hash = v.Hash
    Deprecated = v.Configuration.Deprecated }

let private versionDetails absolutebase ch (v:Version) =
  let mapPathToUrl (path,source) =
    if path="~"
    then (mkResourceUrl absolutebase ch v.Configuration.Version source),""
    else (mkGlobalResourceUrl absolutebase ch path),source

  let rewriteExpectations =
    v.Manifest.Expectations
    |> List.map (function
        | FileExpected (target, resource) ->
          FileExpected (target, { resource with Source = mapPathToUrl resource.Source })
        | ExactFileExpected (target,fhash, resource) ->
          ExactFileExpected (target,fhash,{ resource with Source = mapPathToUrl resource.Source })
        | x -> x)

  { VersionDetails.Version = v.Configuration.SemVer.ToString()
    ReleaseNotes = v.Configuration.ReleaseNotes
    Hash = v.Hash
    Deprecated = v.Configuration.Deprecated
    Manifest = { v.Manifest with Expectations = rewriteExpectations } }

let private lookup repo ch v res =
  let (a,b,c) =
    ( maybe {
        let! channel = repo.Channels |> Map.tryFind ch
        let! version = channel.Versions |> Map.tryFind v
        let r = (version.FilesInRepository |> List.find (fun (a,b,c)->a.Replace("-","")=res))
        return r
      }).Value
  b

let private channelversion absolutebase repo ch v =
  maybe {
    let! channel = repo.Channels |> Map.tryFind ch
    let! version = channel.Versions |> Map.tryFind v
    return versionDetails absolutebase ch version
  }

let private channellatest absolutebase repo ch =
  maybe {
    let! channel = repo.Channels |> Map.tryFind ch
    let! latest = channel.LatestVersion
    return versionDetails absolutebase ch latest
  }

let channel absolutebase (repo:CachedRepository) (ch:string) =
  repo.Channels
  |> Map.tryFind ch
  |> Option.map (fun ch ->
      let versions =
        ch.Versions
        |> Map.toSeq
        |> Seq.sortByDescending (fun (_,v) -> v.Configuration.SemVer)
        |> Seq.map (snd>>(versionSummary absolutebase ch.Name))
        |> Seq.toList
      { ChannelDetails.Channel = ch.Name
        DisplayName = ch.Configuration.Displayname
        Description = ch.Configuration.Description
        LatestVersionUrl = ch.LatestVersion |> Option.map (fun _ -> sprintf "%s/%s" (mkChannelUrl absolutebase ch.Name) "latest")
        Versions = versions } )

let channels absolutebase (repo:CachedRepository) =
  repo.Channels
  |> Map.toSeq
  |> Seq.map (fun (_,ch) ->
      { ChannelSummary.Channel = ch.Name
        DisplayName = ch.Configuration.Displayname
        ChannelDetailsUrl = mkChannelUrl absolutebase ch.Name
        Description = ch.Configuration.Description })
  |> Seq.sortBy (fun l -> l.Channel)
  |> Seq.toList


let jsonContent = Writers.setHeader "Content-Type" "application/json;charset=utf-8"
let jsonResponse data = jsonContent >=> OK (Newtonsoft.Json.JsonConvert.SerializeObject(data,converter))
let jsonResponseOpt info =
  function
  | Some data -> jsonContent >=> OK (Newtonsoft.Json.JsonConvert.SerializeObject(data,converter))
  | None -> RequestErrors.NOT_FOUND (sprintf "Sorry, the requested resource was not found (%s)." info)


[<EntryPoint>]
let main argv =

  if (Array.length argv<>2) then failwith "usage: <executable> http://ipaddr:port path_to_repo"

  let absolutebase = argv.[0]
  let parts = absolutebase.Split(":")
  let pathToRepository = argv.[1]
  let repository = scanRepositoryIntoCache pathToRepository

  let protocol = parts.[0]
  let ipaddress = parts.[1].Trim([|'/'|])
  let port = System.Int32.Parse (parts.[2])
  if (protocol<>"http") then failwithf "Protocol %s not supported" protocol


  let app =
    choose
      [ GET >=> path "/" >=> OK "<h1>ClientDeploy Repository</h1><a href='/repo'>Repository Description</a>"
        GET >=> path "/repo/clientdeploy.zip" >=> Files.sendFile (System.IO.Path.Combine(pathToRepository,"clientdeploy.zip")) false
        GET >=> path "/repo" >=>  jsonResponse ({ RepositoryDescription.APIv1=(sprintf "%s/apiv1" absolutebase) ; CanonicalBaseUrl = absolutebase })
        GET >=> path "/apiv1" >=>  jsonResponse ({ RepositoryAPIv1.UpdaterUrl=null ; ChannelListUrl=(sprintf "%s/channels" absolutebase) })
        GET >=> path "/channels" >=>  jsonResponse (channels absolutebase repository)
        GET >=> pathScan "/channel/%s/latest" (fun (ch) -> jsonResponseOpt "A" (channellatest absolutebase repository ch) )
        GET >=> pathScan "/channel/%s/version/%s/resource/%s.dat" (fun (ch,v,res) -> Files.sendFile (lookup repository ch v res) true )
        GET >=> pathScan "/channel/%s/version/%s" (fun (ch,v) -> jsonResponseOpt "B" (channelversion absolutebase repository ch v) )
        GET >=> pathScan "/channel/%s" (fun ch -> jsonResponseOpt "C" (channel absolutebase repository ch) ) ]
  startWebServer { defaultConfig with bindings = [ HttpBinding.createSimple Protocol.HTTP ipaddress port ] } app
  System.Console.ReadLine() |> ignore
  0