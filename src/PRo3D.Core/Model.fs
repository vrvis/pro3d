﻿namespace PRo3D.Core

type Interactions =
    | PickExploreCenter     = 0
    | PlaceCoordinateSystem = 1  // compute up north vector at that point
    | DrawAnnotation        = 2
    | PlaceRover            = 3
    | TrafoControls         = 4
    | PlaceSurface          = 5
    | PickAnnotation        = 6
    | PickSurface           = 7
    | PickMinervaProduct    = 8
    | PickMinervaFilter     = 9
    | PickLinking           = 10
    | DrawLog               = 11
    | PickLog               = 12
    | PlaceValidator        = 13
    | TrueThickness         = 14 // CHECK-merge