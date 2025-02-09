﻿using SS3D.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;
using SS3D.Utils;
using SS3D.Core;
using System.Linq;

namespace SS3D.Systems.Tile
{
    /// <summary>
    /// Helper class for the tilemap to deal with layers and rotations
    /// </summary>
    public static class TileHelper
    {
        private static TileLayer[] TileLayers;
        
        /// <summary>
        /// Get a direction 90 degree clockwise from the one passed in parameter.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static Direction GetNextCardinalDir(Direction dir)
        {
            switch (dir)
            {
                default:
                case Direction.South: return Direction.West;
                case Direction.SouthWest: return Direction.NorthWest;
                case Direction.West: return Direction.North;
                case Direction.NorthWest: return Direction.NorthEast;
                case Direction.North: return Direction.East;
                case Direction.NorthEast: return Direction.SouthEast;
                case Direction.East: return Direction.South;
                case Direction.SouthEast: return Direction.SouthWest;
            }
        }

        public static Direction GetPreviousCardinalDir(Direction dir)
        {
            return (Direction)MathUtility.mod((int)dir - 2, 8);
        }

        public static Direction GetPreviousDir(Direction dir)
        {
            return (Direction)MathUtility.mod((int)dir - 1, 8);
        }

        public static Direction GetNextDir(Direction dir)
        {
            return (Direction)MathUtility.mod((int)dir + 1, 8);
        }

        /// <summary>
        /// Get the rotation angle of a particular dir.
        /// E.g. assuming north is the initial position (should be), north return 0, north-east 45 ...
        /// </summary>
        public static int GetRotationAngle(Direction dir)
        {
            return (int)dir * 45;
        }

        /// <summary>
        /// Get all different kind of tile layers.
        /// </summary>
        public static TileLayer[] GetTileLayers()
        {
            if (TileLayers == null)
            {
                TileLayers = (TileLayer[])Enum.GetValues(typeof(TileLayer));
            }
            
            return TileLayers;
        }

        /// <summary>
        /// Return the closest cardinal directions from a given other direction.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static List<Direction> ClosestCardinalAdjacentTo(Direction dir)
        {
            if (CardinalDirections().Contains(dir))
            {
                return new() { dir };
            }

            return GetAdjacentDirections(dir);
        }

        public static List<Direction> GetAdjacentDirections(Direction dir)
        {
            return new()
            {
                (Direction)MathUtility.mod((int)dir + 1, 8),
                (Direction)MathUtility.mod((int)dir - 1, 8), 
            };
        }

        /// <summary>
        /// Return the three adjacent directions to Direction dir including itself.
        /// </summary>
        public static List<Direction> GetAdjacentAndMiddleDirection(Direction dir)
        {
            List<Direction> list = new() { dir };
            list.AddRange(GetAdjacentDirections(dir));
            return list;
        }

        /// <summary>
        /// Return the 5 adjacent directions to Direction dir including itself.
        /// </summary>
        public static List<Direction> GetFiveAdjacents(Direction dir)
        {
            return new()
            { 
                dir,
                (Direction)MathUtility.mod((int)dir + 1, 8),
                (Direction)MathUtility.mod((int)dir - 1, 8),
                (Direction)MathUtility.mod((int)dir - 2, 8),
                (Direction)MathUtility.mod((int)dir + 2, 8),
            };
        }

        /// <summary>
        /// Get the offset in coordinates in a given direction.
        /// </summary>
        /// <example>Northwest is (1,-1). East is (1, 0)</example>
        public static Tuple<int, int> ToCardinalVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return new(1, 0);
                case Direction.NorthEast:
                    return new(1, 1);
                case Direction.East:
                    return new(0, 1);
                case Direction.SouthEast:
                    return new(-1, 1);
                case Direction.South:
                    return new(-1, 0);
                case Direction.SouthWest:
                    return new(-1, -1);
                case Direction.West:
                    return new(0, -1);
                case Direction.NorthWest:
                    return new(1, -1);
                default:
                    Log.Error(Subsystems.Get<TileSystem>(), "direction not handled, returning (0,0)");
                    return new(0, 0);
            }
        }

        /// <summary>
        /// Get the closest round number world position on the plane where y = 0.
        /// </summary>
        public static Vector3 GetClosestPosition(Vector3 worldPosition)
        {
            return new(Mathf.Round(worldPosition.x), 0, Mathf.Round(worldPosition.z));
        }
        
        /// <summary>
        /// Get position on the tile grid, that mouse points to.
        /// </summary>
        /// <param name="isTilePosition">If true, position snaps to the center of a tile</param>
        /// <returns></returns>
        public static Vector3 GetPointedPosition(bool isTilePosition = false)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (!new Plane(Vector3.up, 0).Raycast(ray, out float distance))
            {
                return Vector3.zero;
            }

            Vector3 point = ray.GetPoint(distance);
            if (isTilePosition)
            {
                return GetClosestPosition(point);
            }

            return point;
        }

        /// <summary>
        /// Get the relative direction between two direction. 
        /// </summary>
        public static Direction GetRelativeDirection(Direction from, Direction to)
        {
            return (Direction)((((int)from - (int)to) + 8) % 8);
        }

        /// <summary>
        /// Return a list of the cardinal directions.
        /// </summary>
        public static List<Direction> CardinalDirections()
        {
            return new() { Direction.North, Direction.East, Direction.South, Direction.West };
        }

        /// <summary>
        /// Return a list of all existing directions.
        /// </summary>
        public static List<Direction> AllDirections()
        {
            return Enum.GetValues(typeof(Direction)).Cast<Direction>().ToList();
        }

        /// <summary>
        /// Return a list of the diagonal directions.
        /// </summary>
        public static List<Direction> DiagonalDirections()
        {
            return new() { Direction.NorthEast, Direction.SouthEast, Direction.SouthWest, Direction.NorthWest };
        }

        /// <summary>
        /// Return the diagonal direction between two cardinal directions.
        /// </summary>
        public static Direction GetDiagonalBetweenTwoCardinals(Direction cardinal1, Direction cardinal2)
        {
            List<Direction> givenCardinals = new() { cardinal1, cardinal2 };
            return givenCardinals.Contains(Direction.South) ?
                givenCardinals.Contains(Direction.East) ? Direction.SouthEast : Direction.SouthWest :
                givenCardinals.Contains(Direction.West) ? Direction.NorthWest : Direction.NorthEast;
        }

        /// <summary>
        /// Return the cardinal direction between two diagonal directions.
        /// </summary>
        public static Direction GetCardinalBetweenTwoDiagonals(Direction diagonal1, Direction diagonal2)
        {
            List<Direction> givenDiagonals = new() { diagonal1, diagonal2 };
            return givenDiagonals.Contains(Direction.SouthEast) ?
                givenDiagonals.Contains(Direction.NorthEast) ? Direction.East : Direction.South :
                givenDiagonals.Contains(Direction.SouthWest) ? Direction.West : Direction.North;
        }

        /// <summary>
        /// Shortest distance between two directions, in the sense of the number of 45 degree rotation needed
        /// to get from one direction to another.
        /// </summary>
        public static int DistanceBetweenDir(Direction dir1, Direction dir2) 
        {
            int dist1 = MathUtility.mod(dir1 - dir2, 8);
            int dist2 = MathUtility.mod(dir2 - dir1, 8);
            return Math.Min(dist1, dist2);
        }

        /// <summary>
        /// return the closest diagonal from two other directions.
        /// There isn't necessarily a single diagonal which works, take the case of two opposite directions,
        /// then all diagonals work. In case of two adjacent diagonals, it returns the first one.
        /// </summary>
        public static Direction ClosestDiagonalFromTwo(Direction dir1, Direction dir2)
        {
            if (IsDiagonal(dir1) && IsDiagonal(dir2) && DistanceBetweenDir(dir1, dir2) == 2)
            {
                return dir1;
            }

            Direction res = Direction.NorthEast;
            int minDistance = int.MaxValue;
            int distance;
            foreach (Direction diagonal in DiagonalDirections())
            { 
                distance = (int)Math.Pow(DistanceBetweenDir(dir1, diagonal), 2) +
                    (int)Math.Pow(DistanceBetweenDir(dir2, diagonal), 2);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    res = diagonal;
                }
            }

            return res;
        }

        public static bool IsDiagonal(Direction dir)
        {
            return DiagonalDirections().Contains(dir);
        }

        public static bool IsCardinal(Direction dir)
        {
            return CardinalDirections().Contains(dir);
        }

        /// <summary>
        /// Return the angle between two directions, clock wise is positive.
        /// </summary>
        public static float AngleBetween(Direction from, Direction to)
        {
            return ((int)to - (int)from) * 45.0f;
        }

        /// <summary>
        /// Get the opposite direction from the one in parameter.
        /// E.g : North opposite is south. South-East opposite is North-West.
        /// </summary>
        public static Direction GetOpposite(Direction direction)
        {
            return (Direction)(((int)direction + 4) % 8);
        }

        /// <summary>
        /// Return the difference in coordinates for a neighbour tile in front of another one facing
        /// a particular direction.
        /// e.g If the original one is facing north, return (0,1), because, the tile in front of the original
        /// one will be just north of the original one (hence plus one on the y axis).
        /// TODO : isn't "to cardinal vector" method doing the same thing ?
        /// </summary>
        public static Vector2Int CoordinateDifferenceInFrontFacingDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return new(0, 1);
                case Direction.NorthEast:
                    return new(1, 1);
                case Direction.East:
                    return new(1, 0);
                case Direction.SouthEast:
                    return new(1, -1);
                case Direction.South:
                    return new(0, -1);
                case Direction.SouthWest:
                    return new(-1, -1);
                case Direction.West:
                    return new(-1, 0);
                case Direction.NorthWest:
                    return new(-1, 1);
                default:
                    Log.Error(Subsystems.Get<TileSystem>(), "direction not handled, returning (0,0)");
                    return new(0, 0);
            }
        }

        public static bool IsCardinalDirection(Direction dir)
        {
            return (int)dir == 0 || (int)dir == 2 || (int)dir == 4 || (int)dir == 6;
        }

        /// <summary>
        /// Create the right type of tile location depending on the layer it'll be on.
        /// </summary>
        public static ITileLocation CreateTileLocation(TileLayer layer, int x, int y)
        {
            switch (layer)
            {
                case TileLayer.Plenum:
                case TileLayer.Turf:
                case TileLayer.Wire:
                case TileLayer.Disposal:
                case TileLayer.PipeSurface:
                case TileLayer.PipeMiddle:
                case TileLayer.PipeRight:
                case TileLayer.PipeLeft:
                case TileLayer.FurnitureBase:
                case TileLayer.FurnitureTop:
                case TileLayer.Overlays:
                    return new SingleTileLocation(layer, x, y);
                case TileLayer.WallMountHigh:
                case TileLayer.WallMountLow:
                    return new CardinalTileLocation(layer, x, y);
                default:
                    Log.Error(Subsystems.Get<TileSystem>(), $"no objects defined for layer {layer}, add a case to this switch.");
                    return null;
            }
        }
    }
}