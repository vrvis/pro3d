﻿namespace PRo3D.Viewer

open Aardvark.UI.Primitives

type DashboardMode =
    {
        dockConfig : DockConfig
        name       : string
    }

module DashboardModes =
    let comparison =
        {
            name       = "Comparison"
            dockConfig = DockConfigs.comparison
        }
  
    let core =
        {
            name       = "Core"
            dockConfig = DockConfigs.core
        }

    let renderOnly =
        {
            name       = "3D-View Only"
            dockConfig = DockConfigs.renderOnly
        }