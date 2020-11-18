﻿module MazeState

open ResourceIDs
open MazeFilter  // TODO: only needed for the MazeByte type for the array declarations.

type MazeState =
    {
        MazeTilesCountX  : int
        MazeTilesCountY  : int
        MazeTiles        : byte []  // content mutated by pacman eating things   // TODO: strong type wrapper for bytes
        MazeGhostRails   : MazeByte []  // not mutated
        MazePlayersRails : MazeByte []  // not mutated
    }


let private IsSomethingPacManNeedsToEat (tile:byte) =
    (tile = ((byte)TileIndex.Pill1)) || (tile = ((byte)TileIndex.Dot))   // NB: Pill2 is never encoded in the maze itself.

/// Maze property - are all dots and pills eaten?
let IsAllEaten maze =
    not  (maze.MazeTiles |> Array.exists (fun tile -> tile |> IsSomethingPacManNeedsToEat))

