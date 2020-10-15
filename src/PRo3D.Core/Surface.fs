﻿namespace PRo3D.Core

open System
open Aardvark.Base
open Aardvark.VRVis.Opc.KdTrees
open MBrace.FsPickler
open Aardvark.Geometry
open OpcViewer.Base
open FSharp.Data.Adaptive
open OpcViewer.Base.Picking
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Opc
open Aardvark.VRVis.Opc
open PRo3D.Core
open PRo3DCompability

module SurfaceTransformations = 

    let fullTrafo' (surf : Surface) (refsys : ReferenceSystem) = 
    
        let north = refsys.northO.Normalized
        
        let up = refsys.up.value.Normalized
        let east   = north.Cross(up)
              
        let refSysRotation = 
          Trafo3d.FromOrthoNormalBasis(north, east, up)
            
        //translation along north, east, up            
        let trans = surf.transformation.translation.value |> Trafo3d.Translation
        let rot = Trafo3d.Rotation(up, surf.transformation.yaw.value.RadiansFromDegrees())
        
        let originTrafo = -surf.transformation.pivot |> Trafo3d.Translation
        
        originTrafo * rot * originTrafo.Inverse * refSysRotation.Inverse * trans * refSysRotation    
    
    let fullTrafo (surf : aval<AdaptiveSurface>) (refsys : AdaptiveReferenceSystem) = 
        adaptive {
            let! s = surf
            let! s = s.Current
            let! rSys = refsys.Current
            
            return fullTrafo' s rSys
        }
module DebugKdTreesX = 
   
    let getInvalidIndices3f (positions : V3f[]) =
        positions |> List.ofArray |> List.mapi (fun i x -> if x.AnyNaN then Some i else None) |> List.choose id    

    let getTriangleSet3f (vertices:V3f[]) =
        vertices
        |> Seq.map(fun x -> x.ToV3d())
        |> Seq.chunkBySize 3
        |> Seq.filter(fun x -> x.Length = 3)
        |> Seq.map(fun x -> Triangle3d x)
        |> Seq.filter(fun x -> (IntersectionController.triangleIsNan x |> not)) 
        |> Seq.toArray
        |> TriangleSet

    let getTriangleSet (indices : int[]) (vertices:V3d[]) = 
        indices 
        |> Seq.map(fun x -> vertices.[x])
        |> Seq.chunkBySize 3
        |> Seq.map(fun x -> Triangle3d(x))
        |> Seq.filter(fun x -> (IntersectionController.triangleIsNan x |> not)) 
        |> Seq.toArray
        |> TriangleSet

    let loadTriangles (kd : LazyKdTree) =
        
        let positions = kd.objectSetPath |> Aara.fromFile<V3f>
                
        let invalidIndices = getInvalidIndices3f positions.Data |> List.toArray
        let size = positions.Size.XY.ToV2i()
        let indices = PRo3DCSharp.ComputeIndexArray(size, invalidIndices)
                  
       // Log.warn "num of inv_indices: %A" invalidIndices.Length
       // Log.warn "num of indices: %A" indices.Length
                       
        positions.Data 
        |> Array.map (fun x ->  x.ToV3d() |> kd.affine.Forward.TransformPos) 
        |> getTriangleSet indices

    let loadObjectSet (cache : HashMap<string, ConcreteKdIntersectionTree>) (lvl0Tree : Level0KdTree) =             
        match lvl0Tree with
        | InCoreKdTree kd -> 
          kd.kdTree, cache
        | LazyKdTree kd ->             
            let kdTree, cache =
                match kd.kdTree with
                | Some k -> k, cache
                | None -> 
                    let key = (kd.boundingBox.ToString())
                    let tree = cache |> HashMap.tryFind key
                    match tree with
                    | Some t ->                 
                        t, cache
                    | None ->                                     
                        Log.line "cache miss %A- loading kdtree" kd.boundingBox
                    
                        let mutable tree = KdTrees.loadKdtree kd.kdtreePath
                        let triangles = kd |> loadTriangles
                        
                        tree.KdIntersectionTree.ObjectSet <- triangles                                                            
                        tree, (HashMap.add key tree cache)
            kdTree, cache

    let getTriangle (set : TriangleSet) (index : int) : Triangle3d =
        let pi = index * 3
        let pl = set.Position3dList
        Triangle3d(pl.[pi], pl.[pi+1], pl.[pi + 2])

    let isNotOversized (size) (triangle:Triangle3d) =      
      ((Vec.Distance(triangle.P0, triangle.P1) < size) && 
       (Vec.Distance(triangle.P0, triangle.P2) < size) &&
       (Vec.Distance(triangle.P1, triangle.P2) < size))

    let intersectKdTrees bb (hitObject : Surface) (cache : HashMap<string, ConcreteKdIntersectionTree>) (ray : FastRay3d) (kdTreeMap: HashMap<Box3d, KdTrees.Level0KdTree>) = 

        let kdtree, c = kdTreeMap |> HashMap.find bb |> loadObjectSet cache

        let kdi = kdtree.KdIntersectionTree 
        let mutable hit = ObjectRayHit.MaxRange
                        
        try
            let hitFilter = //true means being omitted
                fun (a:IIntersectableObjectSet) (b:int) _ _ -> 
                let triangles = a :?> TriangleSet //TODO TO crashes if not encountering a triangleset
                b |> getTriangle triangles |> isNotOversized hitObject.triangleSize.value |> not // = tooBig            
            
            if kdi.Intersect(ray, null, Func<IIntersectableObjectSet,int,int, RayHit3d,bool>(hitFilter), 0.0, Double.MaxValue, &hit) then              
                Some (hit.RayHit.T, hitObject),c
            else            
                None,c
        with 
          | e -> 
            Log.error "[DebugKdtrees] error in kdtree intersection" 
            Log.error "%A" e
            None,c

module SurfaceIntersection =

    let doKdTreeIntersection 
        (m             : SurfaceModel)
        (refSys        : ReferenceSystem)
        (r             : FastRay3d) 
        (filterSurface : Guid -> Leaf -> SgSurface -> bool) 
        cache
        : option<float * Surface> * HashMap<_,_> = 
        
        let mutable cache = cache
        let activeSgSurfaces = 
            m.surfaces.flat
            |> HashMap.toList 
            |> List.choose (fun (id,leaf) -> 
                match m.sgSurfaces |> HashMap.tryFind id with
                | None -> None
                | Some s -> 
                    if filterSurface id leaf s then Some s
                    else None
            )
            
        let hits = 
            activeSgSurfaces 
            |> List.map (fun d -> d.picking, (m.surfaces.flat |> HashMap.find d.surface) |> Leaf.toSurface)                      
            |> List.choose (fun (p ,surf) ->
                match p with
                | Picking.KdTree kd ->
                    if kd.IsEmpty then Log.error "no kdtree loaded for %s" surf.name; None
                    else                    
                        let superTrafo = SurfaceTransformations.fullTrafo' surf refSys
                        //get bbs that are hit
                        let hitBoxes =
                            kd
                            |> HashMap.toList |> List.map fst
                            |> List.filter(fun x -> 
                                let mutable t = 0.0
                                let r' = r.Ray //combine pre and current transform
                                x.Transformed(surf.preTransform.Forward * superTrafo.Forward).Intersects(r', &t)
                            )
                        //intersect corresponding kdtrees
                        let closestHit =
                            hitBoxes
                            |> List.choose(fun key -> 
                                //Log.line "intersection: %s; pr: %f" surf.name surf.priority.value                                                             
                                //let ray = r.Ray.Transformed(surf.preTransform.Backward) |> FastRay3d  //combine pre and current transform                     
                                let ray = r.Ray.Transformed(surf.preTransform.Backward * superTrafo.Backward) |> FastRay3d
                                let hit, c = 
                                  kd |> DebugKdTreesX.intersectKdTrees key surf cache ray
                                cache <- c
                                hit
                            )
                            |> List.sortBy(fun (t,_)-> t)
                            |> List.tryHead
                        
                        closestHit
                | Picking.PickMesh pm -> Log.error "no kdtree loaded for %s" surf.name; None
            )
            |> List.groupBy(fun (_,surf) -> surf.priority.value) |> List.sortByDescending fst
            |> List.tryHead
            |> Option.bind(fun (_,b) -> b |> List.sortBy fst |> List.tryHead)
            //|> List.sortBy(fun (t,_) -> t)
            //|> List.tryPick(fun d -> Some d)
            
        hits, cache    
