﻿using System;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Mathf = UnityEngine.Mathf;
using Plane = UnityEngine.Plane;
using Debug = UnityEngine.Debug;
using UnitySceneExtensions;
using System.Collections.Generic;

namespace Chisel.Core
{
    // TODO: rename
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateLinearStairs(ref ChiselBrushContainer brushContainer, ref ChiselLinearStairsDefinition definition)
        {
            definition.Validate();

            int requiredSubMeshCount = BrushMeshFactory.GetLinearStairsSubMeshCount(definition, definition.leftSide, definition.rightSide);
            if (requiredSubMeshCount == 0)
                return false;
            
            int subMeshOffset = 0;

            brushContainer.EnsureSize(requiredSubMeshCount);

            return GenerateLinearStairsSubMeshes(ref brushContainer, definition, definition.leftSide, definition.rightSide, subMeshOffset);
        }

        public static bool GenerateLinearStairsSubMeshes(ref ChiselBrushContainer brushContainer, ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition, int subMeshOffset = 0)
        {
            // TODO: properly assign all materials

            if (definition.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
                return false;

            // TODO: implement smooth riser-type

            const float kEpsilon = 0.001f;

            // TODO: put these values in a shared location since they need to match in multiple locations

            var treadHeight     = (definition.treadHeight < kEpsilon) ? 0 : definition.treadHeight;
            var riserType       = (treadHeight == 0 && definition.riserType == StairsRiserType.ThinRiser) ? StairsRiserType.ThickRiser : definition.riserType;
            var leftSideType    = (riserType == StairsRiserType.None && definition.leftSide == StairsSideType.Up) ? StairsSideType.DownAndUp : leftSideDefinition;
            var rightSideType   = (riserType == StairsRiserType.None && definition.rightSide == StairsSideType.Up) ? StairsSideType.DownAndUp : rightSideDefinition;
            if (riserType == StairsRiserType.Smooth)
            {
                switch (leftSideType)
                {
                    case StairsSideType.Up: leftSideType = StairsSideType.DownAndUp; break;
                    case StairsSideType.None: leftSideType = StairsSideType.Down; break;
                }
                switch (rightSideType)
                {
                    case StairsSideType.Up: rightSideType = StairsSideType.DownAndUp; break;
                    case StairsSideType.None: rightSideType = StairsSideType.Down; break;
                }
            }
            var boundsMin = definition.bounds.min;
            var boundsMax = definition.bounds.max;

            if (boundsMin.y > boundsMax.y) { var t = boundsMin.y; boundsMin.y = boundsMax.y; boundsMax.y = t; }
            if (boundsMin.x > boundsMax.x) { var t = boundsMin.x; boundsMin.x = boundsMax.x; boundsMax.x = t; }
            if (boundsMin.z > boundsMax.z) { var t = boundsMin.z; boundsMin.z = boundsMax.z; boundsMax.z = t; }

            var haveRiser           = riserType != StairsRiserType.None;
            var haveLeftSideDown    = riserType != StairsRiserType.FillDown &&
                                      (leftSideType == StairsSideType.Down || leftSideType == StairsSideType.DownAndUp);
            var haveLeftSideUp      = (leftSideType == StairsSideType.Up || leftSideType == StairsSideType.DownAndUp);
            var haveRightSideDown   = riserType != StairsRiserType.FillDown &&
                                      (rightSideType == StairsSideType.Down || rightSideType == StairsSideType.DownAndUp);
            var haveRightSideUp     = (rightSideType == StairsSideType.Up || rightSideType == StairsSideType.DownAndUp);
            var sideWidth           = definition.sideWidth;
            var sideHeight          = definition.sideHeight;
            var leftSideDepth       = (haveLeftSideDown) ? definition.sideDepth : 0;
            var rightSideDepth      = (haveRightSideDown) ? definition.sideDepth : 0;
            var thickRiser          = riserType == StairsRiserType.ThickRiser || riserType == StairsRiserType.Smooth;
            var riserDepth          = (haveRiser && !thickRiser) ? definition.riserDepth : 0;

            var stepCount           = definition.StepCount;
            var offsetZ             = (definition.StepDepthOffset < kEpsilon) ? 0 : definition.StepDepthOffset;
            var offsetY             = definition.plateauHeight;
            var nosingDepth         = definition.nosingDepth;

            var haveTread           = (treadHeight >= kEpsilon);
            var haveTopSide         = (sideHeight > kEpsilon);

            var leftNosingWidth     = haveLeftSideUp ? -sideWidth : definition.nosingWidth;
            var rightNosingWidth    = haveRightSideUp ? -sideWidth : definition.nosingWidth;
            var leftTopNosingWidth  = (haveLeftSideUp && (!haveTopSide)) ? definition.nosingWidth : leftNosingWidth;
            var rightTopNosingWidth = (haveRightSideUp && (!haveTopSide)) ? definition.nosingWidth : rightNosingWidth;

            var subMeshCount        = 0; if (haveRiser) subMeshCount = stepCount;
            var startTread          = subMeshCount; if (haveTread) subMeshCount += stepCount;
            var startLeftSideDown   = subMeshCount; if (haveLeftSideDown) subMeshCount += stepCount;
            var startRightSideDown  = subMeshCount; if (haveRightSideDown) subMeshCount += stepCount;
            var startLeftSideUp     = subMeshCount; if (haveLeftSideUp) subMeshCount += (stepCount - 1) + (haveTopSide ? 1 : 0) + 1;//(haveLeftSideDown  ? 0 : 1);
            var startRightSideUp    = subMeshCount; if (haveRightSideUp) subMeshCount += (stepCount - 1) + (haveTopSide ? 1 : 0) + 1;//(haveRightSideDown ? 0 : 1);

            var stepOffset = new Vector3(0, -definition.stepHeight, definition.stepDepth);
            if (stepCount > 0)
            {
                if (haveRiser)
                {
                    var min = boundsMin;
                    var max = boundsMax;
                    max.z = min.z + definition.StepDepthOffset + definition.stepDepth;
                    if (riserType != StairsRiserType.FillDown)
                    {
                        if (riserType == StairsRiserType.ThinRiser)
                            min.z = max.z - riserDepth;
                        else
                            min.z = min.z + definition.StepDepthOffset;
                        if (thickRiser)
                            min.z -= offsetZ;
                    }
                    min.y = max.y - definition.stepHeight;
                    min.y -= treadHeight;
                    max.y -= treadHeight;
                    min.x += haveRightSideUp ? sideWidth : 0;
                    max.x -= haveLeftSideUp ? sideWidth : 0;
                    var extrusion = new Vector3(max.x - min.x, 0, 0);
                    for (int i = 0; i < stepCount; i++)
                    {
                        if (i == 1 &&
                            thickRiser)
                        {
                            min.z += offsetZ;
                        }
                        if (i == stepCount - 1)
                        {
                            min.y += treadHeight - offsetY;
                        }

                        Vector3[] vertices;
                        if (i == 0 || riserType != StairsRiserType.Smooth)
                        {
                            vertices = new[] {
                                                new Vector3( min.x, min.y, min.z),	// 0
                                                new Vector3( min.x, min.y, max.z),	// 1
                                                new Vector3( min.x, max.y, max.z),  // 2
                                                new Vector3( min.x, max.y, min.z),	// 3
                                            };
                        } else
                        {
                            vertices = new[] {
                                                new Vector3( min.x, min.y, min.z),	// 0
                                                new Vector3( min.x, min.y, max.z),	// 1
                                                new Vector3( min.x, max.y, max.z),  // 2
                                                new Vector3( min.x, max.y, min.z - definition.stepDepth),	// 3
                                            };
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + i], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);

                        if (riserType != StairsRiserType.FillDown)
                            min.z += definition.stepDepth;
                        max.z += definition.stepDepth;
                        min.y -= definition.stepHeight;
                        max.y -= definition.stepHeight;
                    }
                }
                if (haveTread)
                {
                    var min = new Vector3(boundsMin.x + sideWidth, boundsMax.y - definition.treadHeight, boundsMin.z);
                    var max = new Vector3(boundsMax.x - sideWidth, boundsMax.y, boundsMin.z + definition.StepDepthOffset + definition.stepDepth + nosingDepth);
                    for (int i = 0; i < stepCount; i++)
                    {
                        min.x = boundsMin.x - ((i == 0) ? rightTopNosingWidth : rightNosingWidth);
                        max.x = boundsMax.x + ((i == 0) ? leftTopNosingWidth : leftNosingWidth);
                        if (i == 1)
                        {
                            min.z = max.z - (definition.stepDepth + nosingDepth);
                        }
                        var vertices = new[] {
                                                new Vector3( min.x, min.y, min.z),	// 0
                                                new Vector3( min.x, min.y, max.z),	// 1
                                                new Vector3( min.x, max.y, max.z),  // 2
                                                new Vector3( min.x, max.y, min.z),	// 3
                                            };
                        var extrusion = new Vector3(max.x - min.x, 0, 0);
                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + startTread + i], vertices, extrusion,
                                        new int[] { 0, 1, 2, 2, 2, 2 }, // TODO: fix this
                                        definition.surfaceDefinition);
                        min += stepOffset;
                        max += stepOffset;
                    }
                }
                if (haveLeftSideDown)
                {
                    var min = new Vector3(boundsMax.x - sideWidth, boundsMax.y - definition.stepHeight - definition.treadHeight, boundsMin.z + definition.StepDepthOffset);
                    var max = new Vector3(boundsMax.x, boundsMax.y - definition.treadHeight, boundsMin.z + definition.StepDepthOffset + definition.stepDepth);

                    var extrusion = new Vector3(sideWidth, 0, 0);
                    var extraDepth = (thickRiser ? definition.stepDepth : riserDepth) + leftSideDepth;
                    var maxDepth = boundsMin.z;

                    GenerateBottomRamp(ref brushContainer, subMeshOffset + startLeftSideDown, stepCount, min, max, extrusion, riserType, definition.stepDepth - riserDepth, extraDepth, maxDepth, definition, definition.surfaceDefinition);
                }
                if (haveRightSideDown)
                {
                    var min = new Vector3(boundsMin.x, boundsMax.y - definition.stepHeight - definition.treadHeight, boundsMin.z + definition.StepDepthOffset);
                    var max = new Vector3(boundsMin.x + sideWidth, boundsMax.y - definition.treadHeight, boundsMin.z + definition.StepDepthOffset + definition.stepDepth);

                    var extrusion = new Vector3(sideWidth, 0, 0);
                    var extraDepth = (thickRiser ? definition.stepDepth : riserDepth) + rightSideDepth;
                    var maxDepth = boundsMin.z;

                    GenerateBottomRamp(ref brushContainer, subMeshOffset + startRightSideDown, stepCount, min, max, extrusion, riserType, definition.stepDepth - riserDepth, extraDepth, maxDepth, definition, definition.surfaceDefinition);
                }
                if (haveLeftSideUp)
                {
                    var min = new Vector3(boundsMax.x - sideWidth, boundsMax.y - definition.treadHeight - definition.stepHeight, boundsMin.z + definition.StepDepthOffset + definition.stepDepth);
                    var max = new Vector3(boundsMax.x, boundsMax.y - definition.treadHeight, boundsMin.z + definition.StepDepthOffset + definition.stepDepth + definition.stepDepth);
                    var extrusion = new Vector3(sideWidth, 0, 0);
                    var extraDepth = (thickRiser ? definition.stepDepth : riserDepth) + leftSideDepth;
                    var maxDepth = boundsMin.z;

                    GenerateTopRamp(ref brushContainer, subMeshOffset + startLeftSideUp, stepCount - 1, min, max, extrusion, sideHeight, extraDepth, maxDepth, riserType, definition, definition.surfaceDefinition);

                    if (haveTopSide)
                    {
                        var vertices = new[] {
                                            new Vector3( min.x, max.y + sideHeight + definition.treadHeight, min.z),		// 0
                                            new Vector3( min.x, max.y + sideHeight + definition.treadHeight, boundsMin.z),	// 1
                                            new Vector3( min.x, max.y                                      , boundsMin.z),  // 2
                                            new Vector3( min.x, max.y                                      , min.z),		// 3
                                        };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + startLeftSideUp + (stepCount - 1)], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                    //if (!haveLeftSideDown)
                    {
                        var stepHeight = definition.stepHeight;
                        Vector3[] vertices;
                        if (riserType == StairsRiserType.FillDown)
                        {
                            vertices = new[] {
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMax.z),	// 0
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMin.z), // 1
                                            new Vector3( min.x, boundsMin.y             , boundsMin.z), // 2
                                            new Vector3( min.x, boundsMin.y             , boundsMax.z),	// 3
                                        };
                        } else
                        {
                            vertices = new[] {
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMax.z),		// 0
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMax.z - extraDepth),  // 1
                                            new Vector3( min.x, boundsMin.y             , boundsMax.z - extraDepth),  // 2
                                            new Vector3( min.x, boundsMin.y             , boundsMax.z),		// 3
                                        };
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + startLeftSideUp + stepCount], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                }
                if (haveRightSideUp)
                {
                    var min = new Vector3(boundsMin.x, boundsMax.y - definition.treadHeight - definition.stepHeight, boundsMin.z + definition.StepDepthOffset + definition.stepDepth);
                    var max = new Vector3(boundsMin.x + sideWidth, boundsMax.y - definition.treadHeight, boundsMin.z + definition.StepDepthOffset + definition.stepDepth + definition.stepDepth);
                    var extrusion = new Vector3(sideWidth, 0, 0);
                    var extraDepth = (thickRiser ? definition.stepDepth : riserDepth) + rightSideDepth;
                    var maxDepth = boundsMin.z;

                    GenerateTopRamp(ref brushContainer, subMeshOffset + startRightSideUp, stepCount - 1, min, max, extrusion, sideHeight, extraDepth, maxDepth, riserType, definition, definition.surfaceDefinition);

                    if (haveTopSide)
                    {
                        var vertices = new[] {
                                            new Vector3( min.x, max.y + sideHeight + definition.treadHeight, min.z),		// 0
                                            new Vector3( min.x, max.y + sideHeight + definition.treadHeight, boundsMin.z),  // 1
                                            new Vector3( min.x, max.y                                      , boundsMin.z),  // 2
                                            new Vector3( min.x, max.y                                      , min.z),		// 3
                                        };

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + startRightSideUp + (stepCount - 1)], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                    //if (!haveRightSideDown)
                    {
                        var stepHeight = definition.stepHeight;
                        Vector3[] vertices;
                        if (riserType == StairsRiserType.FillDown)
                        {
                            vertices = new[] {
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMax.z),		// 0
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMin.z),  // 1
                                            new Vector3( min.x, boundsMin.y             , boundsMin.z),  // 2
                                            new Vector3( min.x, boundsMin.y             , boundsMax.z),		// 3
                                        };
                        } else
                        {
                            vertices = new[] {
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMax.z),		// 0
                                            new Vector3( min.x, boundsMin.y + stepHeight, boundsMax.z - extraDepth),  // 1
                                            new Vector3( min.x, boundsMin.y             , boundsMax.z - extraDepth),  // 2
                                            new Vector3( min.x, boundsMin.y             , boundsMax.z),		// 3
                                        };
                        }

                        BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[subMeshOffset + startRightSideUp + stepCount], vertices, extrusion,
                                        new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                        definition.surfaceDefinition);
                    }
                }
            }
            return true;
        }

        // TODO: remove all stairs specific parameters
        static void GenerateBottomRamp(ref ChiselBrushContainer brushContainer, int startIndex, int stepCount, Vector3 min, Vector3 max, Vector3 extrusion, StairsRiserType riserType, float riserDepth, float extraDepth, float maxDepth, ChiselLinearStairsDefinition definition, in ChiselSurfaceDefinition surfaceDefinition)
        {
            for (int i = 0, j = startIndex; i < stepCount; i++, j++)
            {
                Vector3[] vertices;
                var z0 = Mathf.Max(maxDepth, min.z - extraDepth);
                var z1 = Mathf.Max(maxDepth, max.z - extraDepth);
                var z2 = Mathf.Max(maxDepth, min.z + riserDepth);/*
                if (z2 < z1)
                {
                    var t = z1; z1 = z2; z2 = t;
                    z1 = Mathf.Max(maxDepth, min.z - extraDepth + definition.stepDepth);
                    z2 = Mathf.Max(maxDepth, max.z);
                }*/
                if (i != stepCount - 1)
                {
                    vertices = new[] {
                                        new Vector3( min.x, max.y, z2), // 0
                                        new Vector3( min.x, max.y, z0), // 1
                                        new Vector3( min.x, min.y, z1), // 2
                                        new Vector3( min.x, min.y, z2), // 3
                                    };
                } else
                {
                    vertices = new[] {  
                                        new Vector3( min.x, max.y, z2), // 0
                                        new Vector3( min.x, max.y, z0), // 1
                                        new Vector3( min.x, min.y + definition.treadHeight, z1), // 2
                                        new Vector3( min.x, min.y + definition.treadHeight, z2), // 3  
                                    };
                }

                BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j], vertices, extrusion,
                                new int[] { 0, 1, 2, 3, 3, 3 }, // TODO: fix this
                                surfaceDefinition);

                min.z += definition.stepDepth;
                max.z += definition.stepDepth;
                min.y -= definition.stepHeight;
                max.y -= definition.stepHeight;
            }
        }

        // TODO: remove all stairs specific parameters
        static void GenerateTopRamp(ref ChiselBrushContainer brushContainer, int startIndex, int stepCount, Vector3 min, Vector3 max, Vector3 extrusion, float sideHeight, float extraDepth, float maxDepth, StairsRiserType riserType, ChiselLinearStairsDefinition definition, in ChiselSurfaceDefinition surfaceDefinition)
        {
            //var diffY			= (max.y - min.y);
            //var diffZ			= (max.z - min.z);
            //var aspect		= diffY / diffZ;
            var diagonalHeight	= sideHeight + definition.treadHeight;//Mathf.Max(sideHeight + definition.treadHeight, (aspect * (riserDepth)));// + definition.nosingDepth)) + definition.treadHeight;

            for (int i = 0, j = startIndex; i < stepCount; i++, j++)
            {
                var topY		= max.y + diagonalHeight;
                var bottomY		= min.y;
                var middleY		= (bottomY + diagonalHeight);// - (lastStep ? (riserDepth * aspect) : 0);
                var rightZ		= Mathf.Max(maxDepth, max.z);// + (lastStep ? riserDepth : 0);
                var leftZ		= Mathf.Max(maxDepth, min.z);

                //            topY leftZ
                //          0    4 
                //           *--*    
                //           |   \
                //           |    \
                //  lefterZ  |     \ 3
                //           |      * 
                //           |      |   rightZ
                //           *------*
                //          1        2
                //            bottomY
                var lefterZ = (i == 0 || (riserType == StairsRiserType.FillDown)) ? maxDepth : Mathf.Max(maxDepth, leftZ - extraDepth);
                var vertices = new[] {
                                    new Vector3( min.x, topY,    lefterZ),   // 0
                                    new Vector3( min.x, bottomY, lefterZ),   // 1
                                    new Vector3( min.x, bottomY, rightZ),  // 2
                                    new Vector3( min.x, middleY, rightZ),  // 3
                                    new Vector3( min.x, topY,    leftZ),   // 4
                                };

                BrushMeshFactory.CreateExtrudedSubMesh(ref brushContainer.brushMeshes[j + 0], vertices, extrusion, 
                                new int[] { 0, 1, 2, 3, 3, 3, 3 }, // TODO: fix this
                                surfaceDefinition);
                        
                min.z += definition.stepDepth;
                max.z += definition.stepDepth;
                min.y -= definition.stepHeight;
                max.y -= definition.stepHeight;							
            }
        }

        public static int GetLinearStairsSubMeshCount(ChiselLinearStairsDefinition definition, StairsSideType leftSideDefinition, StairsSideType rightSideDefinition)
        {
            if (definition.surfaceDefinition.surfaces.Length != (int)ChiselLinearStairsDefinition.SurfaceSides.TotalSides)
            {
                return 0;
            }

            // TODO: implement smooth riser-type
            
            const float kEpsilon = 0.001f;
        
            // TODO: put these values in a shared location since they need to match in multiple locations
            
            var treadHeight			= (definition.treadHeight < kEpsilon) ? 0 : definition.treadHeight;
            var riserType			= (treadHeight == 0 && definition.riserType == StairsRiserType.ThinRiser) ? StairsRiserType.ThickRiser : definition.riserType;
            var leftSideType		= (riserType == StairsRiserType.None && definition.leftSide  == StairsSideType.Up) ? StairsSideType.DownAndUp : leftSideDefinition;
            var rightSideType		= (riserType == StairsRiserType.None && definition.rightSide == StairsSideType.Up) ? StairsSideType.DownAndUp : rightSideDefinition;
            if (riserType == StairsRiserType.Smooth)
            {
                switch (leftSideType)
                {
                    case StairsSideType.Up:   leftSideType = StairsSideType.DownAndUp; break;
                    case StairsSideType.None: leftSideType = StairsSideType.Down; break;
                }
                switch (rightSideType)
                {
                    case StairsSideType.Up:   rightSideType = StairsSideType.DownAndUp; break;
                    case StairsSideType.None: rightSideType = StairsSideType.Down; break;
                }
            }
            
            var haveRiser			= riserType != StairsRiserType.None;
            var haveLeftSideDown	= riserType != StairsRiserType.FillDown &&
                                      (leftSideType == StairsSideType.Down || leftSideType == StairsSideType.DownAndUp);
            var haveLeftSideUp		= (leftSideType == StairsSideType.Up   || leftSideType == StairsSideType.DownAndUp);
            var haveRightSideDown	= riserType != StairsRiserType.FillDown &&
                                      (rightSideType == StairsSideType.Down || rightSideType == StairsSideType.DownAndUp);
            var haveRightSideUp		= (rightSideType == StairsSideType.Up   || rightSideType == StairsSideType.DownAndUp);
            var sideHeight			= definition.sideHeight;  
            

            var stepCount			= definition.StepCount;			
            var haveTread			= (treadHeight >= kEpsilon);
            var haveTopSide			= (sideHeight > kEpsilon);
            
            var subMeshCount		= 0;
            if (haveRiser)			subMeshCount = stepCount;
            if (haveTread)			subMeshCount += stepCount;
            if (haveLeftSideDown)	subMeshCount += stepCount;			
            if (haveRightSideDown)	subMeshCount += stepCount;
            if (haveLeftSideUp)		subMeshCount += (stepCount - 1) + (haveTopSide ? 1 : 0) + 1;//(haveLeftSideDown  ? 0 : 1);
            if (haveRightSideUp)	subMeshCount += (stepCount - 1) + (haveTopSide ? 1 : 0) + 1;//(haveRightSideDown ? 0 : 1);

            return subMeshCount;
        }
    }
}