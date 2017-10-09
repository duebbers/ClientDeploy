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

  type VersionManifest =
    { Expectations : InstallerExpectations list }