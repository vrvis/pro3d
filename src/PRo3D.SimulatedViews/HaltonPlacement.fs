﻿namespace PRo3D.SimulatedViews

open System
open System.IO

open Aardvark.Base
open Aardvark.Base.Geometry
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Operators
open Aardvark.Base.Rendering
open Aardvark.Base.Rendering.Effects
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives
open Aardvark.UI.Trafos
open Aardvark.UI.Animation
open Aardvark.Rendering.Text

open Aardvark.SceneGraph.Opc
open Aardvark.SceneGraph.SgPrimitives.Sg
open Aardvark.GeoSpatial.Opc
open OpcViewer.Base

open Adaptify.FSharp.Core

open PRo3D.Core

module HaltonPlacement =
    let create2DHaltonRandomSeries =
        new HaltonRandomSeries(2, new RandomSystem(System.DateTime.Now.Second))

    let create1DHaltonRandomSeries =
        new HaltonRandomSeries(1, new RandomSystem(System.DateTime.Now.Second))
    
    let genRandomNumbers count =
        let rnd = System.Random()
        List.init count (fun _ -> rnd.Next())
    
    let genRandomNumbersBetween count min max =
        let rnd = System.Random()
        List.init count (fun _ -> rnd.Next(min, max))
        
    
    let computeSCRayRaster (number : int) (view : CameraView) (frustum : Frustum) (haltonRandom : HaltonRandomSeries) =
        [
        for i in [| 0 .. number-1|] do
            let x = frustum.left   + (haltonRandom.UniformDouble(0) * (frustum.right - frustum.left));
            let y = frustum.bottom + (haltonRandom.UniformDouble(1) * (frustum.top - frustum.bottom));
            
            let centralPointonNearPlane = view.Location + (view.Forward * frustum.near)
            let newPointOnNearPlane = centralPointonNearPlane + (view.Right * x) + (view.Up * y)
            let transformedForwardRay = new Ray3d(view.Location, (newPointOnNearPlane - view.Location).Normalized)
    
            yield transformedForwardRay            
        ]  
        

    let mutable lastHash = -1  
    
    let getSinglePointOnSurface 
        (interaction : Interactions) 
        (surfaces    : SurfaceModel) 
        (refSystem   : ReferenceSystem) 
        (cameraLocation : V3d ) 
        (ray : Ray3d) = 

        let mutable cache = HashMap.Empty
        let rayHash = ray.GetHashCode()

        if rayHash = lastHash then
            None
        else    
            let onlyActive (id : Guid) (l : Leaf) (s : SgSurface) = l.active
            let onlyVisible (id : Guid) (l : Leaf) (s : SgSurface) = l.visible

            let surfaceFilter = 
               match interaction with
               | Interactions.PickSurface -> onlyVisible
               | _ -> onlyActive

            Log.startTimed "[RayCastSurface] try intersect kdtree"                                                             
            let hitF (camLocation : V3d) (p : V3d) = 
                let ray =
                    let dir = (p-camLocation).Normalized
                    FastRay3d(camLocation, dir)
                
                match SurfaceIntersection.doKdTreeIntersection surfaces refSystem ray surfaceFilter cache with
                | Some (t,surf), c ->                             
                    cache <- c; ray.Ray.GetPointOnRay t |> Some
                | None, c ->
                    cache <- c; None
                                  
            let result = 
                match SurfaceIntersection.doKdTreeIntersection surfaces refSystem (FastRay3d(ray)) surfaceFilter cache with
                | Some (t,surf), c ->                         
                    cache <- c
                    let hit = ray.GetPointOnRay(t)
                   
                    lastHash <- rayHash
                    match hitF cameraLocation hit with
                    | None -> None
                    | Some projectedPoint -> Some projectedPoint
                | None, _ -> 
                    Log.error "[RayCastSurface] no hit"
                    None
            Log.stop()
            Log.line "done intersecting"
                
            result 

    let getPointsOnSurfaces 
        (interaction : Interactions) 
        (surfaces    : SurfaceModel)
        (refSystem   : ReferenceSystem) 
        (camLocation : V3d )
        (rays        : list<Ray3d>) =

        rays |> List.choose( fun ray -> getSinglePointOnSurface interaction surfaces refSystem camLocation ray)
        
    let getHaltonRandomTrafos
        (interaction : Interactions) 
        (surfaces    : SurfaceModel) 
        (refSystem   : ReferenceSystem) 
        (shattercone : SnapshotShattercone) 
        (frustum : Frustum) 
        (view : CameraView) =

        let haltonSeries = create2DHaltonRandomSeries
        let rays = computeSCRayRaster shattercone.count view frustum haltonSeries
        let points = getPointsOnSurfaces interaction surfaces refSystem view.Location rays

        let hsScaling = 
            match shattercone.scale with
            | Some s -> 
                let rs = genRandomNumbersBetween shattercone.count s.X s.Y
                rs |> List.map(fun x -> (float)x/100.0) 

            | None -> 
                [ for i in 1 .. shattercone.count -> 1.0 ]

        let xRotation =
            match shattercone.xRotation with
            | Some rx -> genRandomNumbersBetween shattercone.count rx.X rx.Y
            | None -> [ for i in 1 .. shattercone.count -> 0 ]
        
        //let yRotation = genRandomNumbersBetween shattercone.count 45 135
        let yRotation = 
            match shattercone.yRotation with
            | Some ry -> genRandomNumbersBetween shattercone.count ry.X ry.Y
            | None -> [ for i in 1 .. shattercone.count -> 0 ]

        let zRotation =
            match shattercone.zRotation with
            | Some rz -> genRandomNumbersBetween shattercone.count rz.X rz.Y //0 360
            | None -> [ for i in 1 .. shattercone.count -> 0 ]
        //let zRotation = genRandomNumbersBetween shattercone.count 0 360

        let trafos =
            [
            for i in 0..points.Length-1 do
                yield Trafo3d.Scale(float hsScaling.[i]) * 
                Trafo3d.RotationZInDegrees(float zRotation.[i]) *
                Trafo3d.RotationYInDegrees(float yRotation.[i]) *
                Trafo3d.RotationXInDegrees(float xRotation.[i]) *
                Trafo3d.Translation(points.[i])
            ]
            

        points, trafos //points |> List.map( fun p -> Trafo3d.Scale(0.03) * Trafo3d.Translation(p) )
