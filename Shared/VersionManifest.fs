namespace ClientDeploy

module Manifests =

  let INSTALLTARGET = "%installtarget%"

  type Hash = string

  type Resource =
    { Source : string*string
      Size : int
      Hash : Hash }

  type InstallerExpectations =
    | FolderExpected of string
    | FileExpected of string * Resource
    | ExactFileExpected of string * Hash * Resource
    //| ConfigFileExists of string * string
    //| ProcessRuns of string * string * bool // (override command line)
    //| AutoStartLink of string * string * string

  type VersionManifest =
    { Expectations : InstallerExpectations list }