namespace ClientDeploy

module RepoClient =
  open Newtonsoft.Json
  open System.Net

  exception TransientError of string
  exception IncompatibleRepository of string
  
  let private converter = Fable.JsonConverter()
  let private deserialize<'t> json : 't = Newtonsoft.Json.JsonConvert.DeserializeObject<'t>(json,converter)

  let private downloadAndDeserialize<'t> (wc:System.Net.WebClient) (url:string) (errorinfo:string) : 't =
    try
      wc.DownloadString url |> deserialize<'t>
    with
      | :? WebException as ex -> raise (TransientError (sprintf "Error accessing repository '%s' at '%s' (%s)" errorinfo url (sprintf "%s (%s)" ex.Message (ex.GetType().Name))))
      | ex -> raise (IncompatibleRepository (sprintf "Error accessing repository '%s' at '%s' (%s)" errorinfo url (sprintf "%s (%s)" ex.Message (ex.GetType().Name))))

  let private getRepoDescription (wc:System.Net.WebClient) (repobase:string) =    
    downloadAndDeserialize<Resources.RepositoryDescription> wc repobase "repository description"

  let private getRepoAPIv1 (wc:System.Net.WebClient) (repo:Resources.RepositoryDescription) : Resources.RepositoryAPIv1 =    
    downloadAndDeserialize<Resources.RepositoryAPIv1> wc repo.APIv1 "repository API (v1)"

  let private getUpdater (wc:System.Net.WebClient) (api:Resources.RepositoryAPIv1) : Resources.UpdaterDescription =    
    downloadAndDeserialize<Resources.UpdaterDescription> wc api.UpdaterUrl ("updater description")

  let private getChannelList (wc:System.Net.WebClient) (api:Resources.RepositoryAPIv1) : Resources.ChannelList =    
    downloadAndDeserialize<Resources.ChannelList> wc api.ChannelListUrl ("channel list")

  let private getChannelInfo (wc:System.Net.WebClient) (channel:Resources.ChannelSummary) : Resources.ChannelDetails =    
    downloadAndDeserialize<Resources.ChannelDetails> wc channel.ChannelDetailsUrl (sprintf "channel details for %s" channel.Channel)

  let private getVersionInfo (wc:System.Net.WebClient) (url:string) : Resources.VersionDetails =    
    downloadAndDeserialize<Resources.VersionDetails> wc url (sprintf "version details for")


  let updater_version repo : Semver.SemVersion = 
    use wc = new System.Net.WebClient()
    let repo = getRepoDescription wc repo
    let api = getRepoAPIv1 wc repo
    let updater = getUpdater wc api
    Semver.SemVersion.Parse(updater.UpdaterVersion,false)

  let latest_version_information repo product = 
    use wc = new System.Net.WebClient()
    let repo = getRepoDescription wc repo
    let api = getRepoAPIv1 wc repo
    let channels = getChannelList wc api
    match channels |> List.tryFind(fun ch -> ch.Channel = product) with
    | Some channel ->
      let info = getChannelInfo wc channel
      match info.LatestVersionUrl with
      | Some versionurl -> 
        getVersionInfo wc versionurl
      | None -> raise (TransientError (sprintf "no version available for channel %s" product))
    | None ->
      raise (TransientError (sprintf "unable to find channel %s" product))

  let latest_version repo product : Semver.SemVersion = 
    use wc = new System.Net.WebClient()
    let repo = getRepoDescription wc repo
    let api = getRepoAPIv1 wc repo
    let channels = getChannelList wc api
    match channels |> List.tryFind(fun ch -> ch.Channel = product) with
    | Some channel ->
      let info = getChannelInfo wc channel
      match info.LatestVersionUrl with
      | Some versionurl -> 
        let version = getVersionInfo wc versionurl
        Semver.SemVersion.Parse(version.Version,false)
      | None -> raise (TransientError (sprintf "no version available for channel %s" product))
    | None ->
      raise (TransientError (sprintf "unable to find channel %s" product))

