namespace ClientDeploy

module RepoParser =

  type DotVersionConfigFileStructure =
    { Version : string
      SemVer : Semver.SemVersion
      Deprecated : bool
      ReleaseNotes : string }

  type DotChannelConfigFileStucture =
    { Name:string
      Displayname : string
      Hidden : bool
      Description : string }

  type DotRepositoryConfigFileStructure =
    { Key : string }

  type CachedRepository =
    { RootFolder : string
      Configuration : DotRepositoryConfigFileStructure
      Channels : Map<string,Channel> }

  and Channel =
    { Name : string
      Configuration : DotChannelConfigFileStucture
      LatestVersion : Version option
      Versions : Map<string,Version> }

  and Version =
    { Configuration : DotVersionConfigFileStructure
      FilesInRepository : (string*string*int) list
      Hash : string
      Manifest : Manifests.VersionManifest }


  let private converter = Fable.JsonConverter()

  let private combine path file = System.IO.Path.Combine(path,file)

  let private repoconfig basedir = combine basedir ".clientdeployrepo.config"
  let private channelconfig basedir = combine basedir ".config"
  let private versionconfig basedir = combine basedir ".version"

  let private readJson<'t> filename =
    let json = System.IO.File.ReadAllText filename
    Newtonsoft.Json.JsonConvert.DeserializeObject<'t>(json,converter)


  let hasher = System.Security.Cryptography.MD5.Create()

  let hashFile f =
    let data = System.IO.File.ReadAllBytes f
    let hash = hasher.ComputeHash data
    System.Guid(hash).ToString(),data.Length

  let private scanFiles path =
    System.IO.Directory.GetFiles(path, "*.*")
    |> Seq.filter (fun f -> not (System.IO.Path.GetFileName(f).StartsWith(".")))
    |> Seq.map (fun f -> let (hash,size) = (hashFile f) in (hash,f,size))
    |> Seq.toList

  let private scanVersion path =
    printfn "scanning %s" path
    let config = versionconfig path
    if (not (System.IO.File.Exists(config)))
      then None
      else
        let config = readJson<DotVersionConfigFileStructure> config
        let config = { config with SemVer = Semver.SemVersion.Parse(config.Version) }
        let config = { config with Version = config.SemVer.ToString() }
        let files = scanFiles path

        let manifest =
          [ Manifests.FolderExpected Manifests.PathPlaceholders.INSTALLTARGET ] @
          ( files |>
            List.map (fun (hash,file,size) ->
                let filename = System.IO.Path.Combine(Manifests.PathPlaceholders.INSTALLTARGET,System.IO.Path.GetRelativePath(path,file))
                let resource =
                  { Manifests.Resource.Source="~",hash ; Manifests.Resource.Hash=hash ; Manifests.Resource.Size = size }
                Manifests.ExactFileExpected (filename,hash,resource)))

        let version =
          { Version.Hash=""
            Configuration = config
            FilesInRepository = files
            Manifest = { Manifests.VersionManifest.Expectations = manifest } }
        let json = Newtonsoft.Json.JsonConvert.SerializeObject(version,converter)
        let hash = hasher.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))
        let id = System.Guid(hash)
        Some { version with Hash = id.ToString() }



  let private scanVersions basedir =
    System.IO.Directory.GetDirectories(basedir)
    |> Seq.choose scanVersion |> Seq.toList

  let private scanChannel path =
    printfn "scanning %s" path
    let config = channelconfig path
    if (not (System.IO.File.Exists(config)))
      then None
      else
        let config = readJson<DotChannelConfigFileStucture> config
        let versionlist = scanVersions path
        let versions =
          versionlist
          |> List.map (fun v -> v.Configuration.Version,v)
          |> Map.ofList
        let latest = versionlist |> Seq.sortByDescending (fun v -> v.Configuration.SemVer) |> Seq.tryHead
        Some
          { Channel.Name = config.Name
            Configuration = config
            LatestVersion = latest
            Versions = versions }


  let private scanChannels basedir =
    System.IO.Directory.GetDirectories(basedir)
    |> Seq.choose scanChannel |> Seq.toList

  let scanRepositoryIntoCache (basedir:string) : CachedRepository =
    if (not (System.IO.Directory.Exists basedir)) then failwith (sprintf "Repository folder '%s' not found [CS-001]." basedir)

    let config = repoconfig basedir
    if (not (System.IO.File.Exists config)) then failwith (sprintf "Repository folder '%s' is not a valid ClientDeploy Repository [CS-002]." basedir)
    let config = readJson<DotRepositoryConfigFileStructure> config
    let channels =
      scanChannels basedir
      |> List.map (fun ch -> ch.Name,ch)
      |> Map.ofList

    { RootFolder = basedir
      Configuration = config
      Channels = channels }