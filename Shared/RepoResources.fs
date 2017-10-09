namespace ClientDeploy

module Resources =

  type VersionDetails =
    { Version : string
      ReleaseNotes : string
      Hash : string
      Deprecated : bool
      Manifest : Manifests.VersionManifest }

  type VersionSummary =
    { Version : string
      VersionDetailsUrl : string
      ReleaseNotes : string
      Hash : string
      Deprecated : bool }

  type ChannelDetails =
    { Channel : string
      DisplayName : string
      Description : string
      LatestVersionUrl : string option
      Versions : VersionSummary list }

  type ChannelSummary =
    { Channel : string
      DisplayName : string
      ChannelDetailsUrl : string
      Description : string }

  type ChannelList = ChannelSummary list

  type UpdaterDescription =
    { UpdaterVersion : string
      UpdaterResourceUrl : string }

  type RepositoryAPIv1 =
    { UpdaterUrl : string
      ChannelListUrl : string }

  type RepositoryDescription =
    { CanonicalBaseUrl : string
      APIv1 : string }