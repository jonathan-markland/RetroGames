﻿module ScreenPacman

open Time
open DrawingFunctions
open ScoreHiScore
open Geometry
open ResourceIDs
open StaticResourceAccess
open ScreenHandler
open ImagesAndFonts
open PacmanShared
open Input
open MazeFilter
open Rules
open Mazes
open ScreenIntermissions
open Random
open GhostDirectionChoosing



// TODO: Research - a pure functional pacman maze instead of the array mutability.


// TODO: Move these to a library? Are the really generally useful?

/// If the condition is true, return the transformed 
/// object, else return the object unchanged.
let inline UpdateIf condition transformed objekt =   // TODO: move to library
    if condition then objekt |> transformed else objekt



/// If, when applied to the object, the condition is true, 
/// return the transformed object, else return the object unchanged.
let inline UpdateWhen condition transformed objekt =   // TODO: move to library
    if objekt |> condition then objekt |> transformed else objekt


let inline UpdateToValueWhen condition value objekt =   // TODO: move to library
    if objekt |> condition then value else objekt



// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

type private MazeState =
    {
        MazeTilesCountX  : int
        MazeTilesCountY  : int
        MazeTiles        : byte[]  // content mutated by pacman eating things   // TODO: strong type wrapper for bytes
        MazeGhostRails   : byte[]  // not mutated
        MazePlayersRails : byte[]  // not mutated
    }

type private PacmanScreenModel =  // TODO: Getting fat with things that don't change per-frame
    {
        Random                 : XorShift32State
        LevelNumber            : int
        ScoreAndHiScore        : ScoreAndHiScore
        MazeState              : MazeState
        PacmanState            : PacmanState
        GhostsState            : GhostState list
        WhereToOnGameOver      : ScoreAndHiScore -> ErasedGameState
        WhereToOnAllEaten      : int -> ScoreAndHiScore -> float32<seconds> -> ErasedGameState
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

type FindResult = InvalidMaze | NotFound | Found of int * int | NotUnique

let FindUnique charToFind mazeArray =

    let result =
        mazeArray |> IfValidStringRectangleThen (fun width height ->

            let mutable findResult = NotFound

            mazeArray |> List.iteri (fun y row ->
                for x in 0..(width-1) do
                    if row.[x] = charToFind then
                        findResult <-
                            match findResult with
                                | NotFound    -> Found(x,y)
                                | Found _     -> NotUnique
                                | NotUnique   -> NotUnique
                                | InvalidMaze -> failwith "unexpected case"  // should never happen
            )
            Some findResult
        )

    match result with
        | Some findResult -> findResult
        | None -> InvalidMaze

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

type private UnpackedMaze =
    {
        UnpackedMazeState      : MazeState
        UnpackedPacmanPosition : Point<int<epx>>
        UnpackedGhostPositions : Point<int<epx>> list
    }

let private TextMazeDefinitionUnpacked mazeList = // TODO: move to a module?

    // TODO: The test framework must run this over all of the included mazes
    //       and see that it doesn't throw the exceptions.

    // TODO: The rails might return the "central dot"

    // TODO: Have I been too hasty assuming the characters will never encounter
    //       rails with a direction byte mask 0uy?

    let combinedWithDotsAndPillsIn (mazeList:string list) (justTheWalls:byte[]) =

        mazeList |> IfValidStringRectangleThen (fun width height ->

            if (width * height) = justTheWalls.Length then
                let newArray = Array.create justTheWalls.Length ((byte)TileIndex.Blank)
                mazeList |> List.iteri (fun y row ->
                    for x in 0..(width-1) do
                        let i = y * width + x
                        let wall = justTheWalls.[i]
                        let tileIndex =
                            if wall <> 0uy then
                                wall + ((byte)TileIndex.Blank)
                            else
                                let ch = row.[x]
                                if ch = '.' then
                                    ((byte) TileIndex.Dot)
                                else if ch = '@' then
                                    ((byte) TileIndex.Pill1)
                                else
                                    0uy
                        newArray.[i] <- tileIndex
                )
                Some newArray
            else
                None
        )        

    let unwrap errorMessage opt =
        match opt with
            | Some thing -> thing
            | None       -> failwith errorMessage

    let RemoveMazeByteWrapper (MazeByte mazeByte) = mazeByte // TODO: Decide what type wrapping I'm doing here.  This just restores us to the byte-based system currently in wider use.

    let justTheWalls =
        mazeList 
            |> StringArrayToMazeArray (fun ch -> ch = '#' || ch = ':') RemoveMazeByteWrapper 0uy
            |> unwrap "Failed to unpack walls from maze definition, please check text format input."

    let theGhostRails =
        mazeList 
            |> StringArrayToMazeArray (fun ch -> ch <> '#')  RemoveMazeByteWrapper 0uy
            |> unwrap "Failed to unpack rails from maze definition, please check text format input."

    let thePlayersRails =
        mazeList 
            |> StringArrayToMazeArray (fun ch -> ch <> '#' && ch <> ':')  RemoveMazeByteWrapper 0uy
            |> unwrap "Failed to unpack rails from maze definition, please check text format input."

    let theWallsAndPills =
        justTheWalls 
            |> combinedWithDotsAndPillsIn mazeList
            |> unwrap "Failed to obtains the dots and pills from maze definition, please check text format input."

    let unpackFindResult (ch:char) findResult =
        match findResult with
            | InvalidMaze -> failwith "Invalid maze definition"
            | NotFound    -> failwith (sprintf "Could not find char '%s' in the maze definition." (ch.ToString()))
            | NotUnique   -> failwith (sprintf "Char '%s' is not unique in the maze definition." (ch.ToString()))
            | Found(x,y)  -> 
                { ptx = x * TileSide ; pty = y * TileSide }

    let ghostPositions =
        [1..4] |> List.map (fun n -> 
            let ghostChar = (char) (48 + n)
            mazeList |> FindUnique ghostChar |> unpackFindResult ghostChar)

    let pacPosition =
        mazeList |> FindUnique '0' |> unpackFindResult '0'

    {
        UnpackedMazeState =
            {
                MazeTilesCountX  = mazeList.[0].Length  // known valid
                MazeTilesCountY  = mazeList.Length
                MazeTiles        = theWallsAndPills
                MazeGhostRails   = theGhostRails
                MazePlayersRails = thePlayersRails
            }
        UnpackedPacmanPosition = pacPosition
        UnpackedGhostPositions = ghostPositions
    }

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let private OriginForMazeOfDimensions cx cy (countX:int) (countY:int) =

    let x = cx - ((countX * TileSide) / 2)
    let y = cy - ((countY * TileSide) / 2)

    (x,y)

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  Support functions
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let KeysFrom keyStateGetter =
    let up    = (keyStateGetter (WebBrowserKeyCode 38)).Held
    let down  = (keyStateGetter (WebBrowserKeyCode 40)).Held
    let left  = (keyStateGetter (WebBrowserKeyCode 37)).Held
    let right = (keyStateGetter (WebBrowserKeyCode 39)).Held
    struct (up, down, left, right)

/// Asks whether the given pacman or ghost position is aligned
/// to cover a single tile.
let IsAlignedOnTile position =

    let {ptx=x ; pty=y} = position
    let isAligned n = ((n |> IntEpxToInt) % TileSideInt) = 0

    x |> isAligned && y |> isAligned

/// For a precise pacman or ghost position that is precisely over
/// a single tile, this returns the rails array index of that tile.
/// If the position not perfectly aligned on a single tile, this
/// returns None.
let TileIndexOf position =  // TODO: strongly type the return value

    if position |> IsAlignedOnTile then
        let {ptx=x ; pty=y} = position
        let txi = x / TileSide
        let tyi = y / TileSide
        Some (txi, tyi)

    else
        None

/// The railsByte defines permissable directions, and we return
/// true if movement in the given direction is allowed by the railsByte.
let IsDirectionAllowedBy railsByte facingDirection =

    (railsByte &&& (facingDirection |> FacingDirectionToBitMaskByte)) <> 0uy


/// Obtain the *inner* collision rectangle given the top left
/// position of a tile-sized rectangle.
let CollisionRectangle position =
    let x = position.ptx
    let y = position.pty
    let border = (TileSide - CollisionSide) / 2
    {
        Left   = x + border
        Top    = y + border
        Right  = (x + TileSide) - border
        Bottom = (y + TileSide) - border
    }


/// Obtain the bounding rectangle of a tile sized
/// rectangle with its top left at a given pixel position.
let TileBoundingRectangle position =
    let x = position.ptx
    let y = position.pty
    {
        Left   = x
        Top    = y
        Right  = x + TileSide
        Bottom = y + TileSide
    }


/// Obtain the mode of pacman.
let inline PacMode pacman =
    pacman.PacState2.PacMode


/// Obtain pacman's collision rectangle.
let inline PacCollisionRectangle pacman =
    pacman.PacPosition |> CollisionRectangle


/// Update pacman's mode.
let WithPacMode mode pacman =
    {
        PacPosition = pacman.PacPosition
        PacState2 =
            {
                PacMode            = mode
                PacFacingDirection = pacman.PacState2.PacFacingDirection
                LivesLeft          = pacman.PacState2.LivesLeft
                PacHomePosition    = pacman.PacState2.PacHomePosition
            }
    }

    

/// Obtain the mode of the given ghost.
let inline GhostMode ghost =
    ghost.GhostState2.GhostMode


/// Obtain pacman's collision rectangle.
let inline GhostCollisionRectangle ghost =
    ghost.GhostPosition |> CollisionRectangle


/// Update the mode of the given ghost.
let WithGhostMode mode ghost =
    {
        GhostPosition = ghost.GhostPosition
        GhostState2   =
            {
                GhostNumber          = ghost.GhostState2.GhostNumber
                GhostHomePosition    = ghost.GhostState2.GhostHomePosition
                GhostFacingDirection = ghost.GhostState2.GhostFacingDirection
                GhostMode            = mode
                GhostDirectionChoiceProbabilities = ghost.GhostState2.GhostDirectionChoiceProbabilities
            }
    }


/// Asks whether the given ghost is at its base home position.
let inline IsAtHomePosition ghost =
    ghost.GhostPosition = ghost.GhostState2.GhostHomePosition


/// Asks whether a pacman-maze-tile sized area overlaps any
/// of the GhostNormal-state ghosts in the list.  Ghosts in
/// other states never participate in intersection.
/// The tilePos denotes the top left corner.
let IntersectsNormalGhostsIn ghostStateList rect =

    ghostStateList 
        |> List.exists (fun ghost ->
            match ghost.GhostState2.GhostMode with
                | GhostNormal -> ghost |> GhostCollisionRectangle |> RectangleIntersects rect
                | GhostEdibleUntil _
                | GhostRegeneratingUntil _
                | GhostReturningToBase -> false)



/// Wrapping function for pacman and ghosts for when 
/// passages exit the sides of the grid.
let private PointWrappedAtMazeEdges mazeState point =

    let { ptx=x ; pty=y } = point

    let width  = mazeState.MazeTilesCountX * TileSide
    let height = mazeState.MazeTilesCountY * TileSide

    let h = TileSide / 2

    let x = 
        if x < -h then x + width
        else if x >= (width-h)  then x - width
        else x

    let y = 
        if y < -h then y + height 
        else if y >= (height-h) then y - height
        else y

    { ptx=x ; pty=y }




// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  CORRIDOR DETERMINATION
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

/// Returns the corridor rectangle starting from a given maze tile 'originTile',
/// where originTile is a 2D array index into the tiles matrix.  Takes the actual
/// maze, not the rails as the mazeByteArray.
let CorridorRectangle tilesHorizontally tilesVertically (mazeByteArray:byte[]) originTile direction =  // TODO: originTile's unit is not clear

    let stepDelta =
        direction |> DirectionToMovementDelta 0 1

    let isWall (t:byte) =
        t >= ((byte)TileIndex.Wall0) && t <= ((byte)TileIndex.Wall15)
    
    let hasNoExitInDirectionOfTravel pos =  // Not strictly correct, will cause inclusion of the wall square hit, but that is benign for our purposes.
        isWall (mazeByteArray.[pos.pty * tilesHorizontally + pos.ptx])

    let noSquareExistsAt pos =
        pos.ptx < 0 || pos.pty < 0 || pos.ptx >= tilesHorizontally || pos.pty >= tilesVertically

    let boundingRectangleOfSquareAt pos =
        pos |> PointMult TileSide |> TileBoundingRectangle

    let rec stepper  stepDelta position accumulator =
        
        let nextPosition = position |> PointMovedByDelta stepDelta

        if noSquareExistsAt nextPosition || position |> hasNoExitInDirectionOfTravel then
            accumulator
        else
            let r = boundingRectangleOfSquareAt nextPosition
            let union = TightestBoundingRectangleOf r accumulator
            stepper  stepDelta nextPosition union

    stepper stepDelta originTile (boundingRectangleOfSquareAt originTile)

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  DRAWING
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let private DrawSpecificMazeCentred render tilesImage cx cy countX countY (mazeByteArray:byte[]) gameTime =

    let (x,y) = OriginForMazeOfDimensions cx cy countX countY

    for ty in 0..countY - 1 do
        let y' = y + ty * TileSide

        for tx in 0..countX - 1 do
            let tileIndex = mazeByteArray.[ty * countX + tx]
            let x' = x + tx * TileSide
            DrawPacTileInt render tilesImage x' y' ((int)tileIndex) gameTime

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let private DrawMazeCentred render image cx cy mazeState gameTime =

    DrawSpecificMazeCentred 
        render image cx cy 
        mazeState.MazeTilesCountX
        mazeState.MazeTilesCountY
        mazeState.MazeTiles
        gameTime

    // To show the ghost rails:
    // DrawSpecificMazeCentred 
    //     render image cx cy 
    //     mazeState.MazeTilesCountX
    //     mazeState.MazeTilesCountY
    //     mazeState.MazeGhostRails
    //     gameTime

    // To show the player rails:
    // DrawSpecificMazeCentred 
    //     render image cx cy 
    //     mazeState.MazeTilesCountX
    //     mazeState.MazeTilesCountY
    //     mazeState.MazePlayersRails
    //     gameTime

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

/// A debugging facility to render rectangles returned by the CorridorRectangle function.
/// For debug purposes, pacman's position and direction are used as the reference point
/// and corridor search direction.
let private DrawCorridorFinderResult render centreX centreY countX countY mazeByteArray position facing =

    let (x,y) = OriginForMazeOfDimensions centreX centreY countX countY

    let pos    = { ptx=position.ptx / TileSide ; pty=position.pty / TileSide }
    let origin = { modx=x ; mody=y }
    let r      = CorridorRectangle countX countY mazeByteArray pos facing |> RectangleMovedByDelta origin

    let shape =
        DrawingShapes.DrawFilledRectangle (
            r.Left, r.Top, (r |> RectangleWidth), (r |> RectangleHeight), (DrawingShapes.SolidColour 0xFF00FFu))

    render shape

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let private RenderPacmanScreen render (model:PacmanScreenModel) gameTime =

    let backgroundImage = BackgroundImageID |> ImageFromID
    Image1to1 render 0<epx> 0<epx> backgroundImage

    let tilesImage = Level1ImageID |> ImageFromID

    let cx,cy = (ScreenWidthInt / 2) , (ScreenHeightInt / 2) 

    DrawMazeCentred 
        render tilesImage 
        cx cy
        model.MazeState
        gameTime

    // DrawCorridorFinderResult 
    //     render cx cy 
    //     model.MazeState.MazeTilesCountX 
    //     model.MazeState.MazeTilesCountY
    //     model.MazeState.MazeTiles
    //     model.PacmanState.PacPosition
    //     model.PacmanState.PacState2.PacFacingDirection

    let (originx,originy) = 
        OriginForMazeOfDimensions 
            cx cy 
            model.MazeState.MazeTilesCountX 
            model.MazeState.MazeTilesCountY

    let pos = model.PacmanState.PacPosition 
                |> PointWrappedAtMazeEdges model.MazeState
                |> OffsetByOrigin originx originy 
    
    let direction = model.PacmanState.PacState2.PacFacingDirection

    match model.PacmanState.PacState2.PacMode with
        | PacAlive ->
            let drawPacMode = if model.GhostsState |> InPillMode then DrawPacPillMode else DrawPacNormal
            DrawPacManAlive render tilesImage pos direction drawPacMode gameTime

        | PacDyingUntil _ ->
            if gameTime |> PulseActiveAtRate PacmanDyingFlashRate then
                DrawPacManAlive render tilesImage pos direction DrawPacZapped gameTime
            else
                () // No graphics desired.

        | PacDead ->
            () // No graphics desired.

    model.GhostsState
        |> List.iteri (fun i ghostState ->

            // DrawCorridorFinderResult 
            //     render cx cy 
            //     model.MazeState.MazeTilesCountX 
            //     model.MazeState.MazeTilesCountY
            //     model.MazeState.MazeTiles
            //     ghostState.GhostPosition
            //     ghostState.GhostState2.GhostFacingDirection

            let pos =
                ghostState.GhostPosition
                    |> PointWrappedAtMazeEdges model.MazeState
                    |> OffsetByOrigin originx originy
                        
            let number = ghostState.GhostState2.GhostNumber
            let mode   = ghostState.GhostState2.GhostMode

            DrawGhost render tilesImage pos number mode gameTime)

    let indent = 20<epx>
    let indentY = 2<epx>

    Text render GreyFontID LeftAlign  TopAlign    indent indentY (sprintf "SCORE %d" model.ScoreAndHiScore.Score)  // TODO: memoize score to avoid garbage
    Text render GreyFontID RightAlign TopAlign    (ScreenWidthInt - indent) indentY (sprintf "HISCORE %d" model.ScoreAndHiScore.HiScore)  // TODO: memoize score to avoid garbage
    Text render GreyFontID LeftAlign  BottomAlign indent (ScreenHeightInt - indentY) (sprintf "FRAME %d" model.LevelNumber)  // TODO: memoize score to avoid garbage
    Text render GreyFontID RightAlign BottomAlign (ScreenWidthInt - indent) (ScreenHeightInt - indentY) (sprintf "LIVES %d" model.PacmanState.PacState2.LivesLeft)  // TODO: memoize score to avoid garbage



// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  PAC MAN himself:  State advance per frame
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

[<Struct>]
type PacHasEaten =
    | EatenNothing
    | EatenDot of dotTileArrayIndex:int
    | EatenPowerPill of powerPillTileArrayIndex:int

/// Handles all the positive things for pac man:  Moving and eating.
/// Returns an indicator of what happened.
let private AdvancePacMan keyStateGetter mazeState pacmanState =
    
    let position  = pacmanState.PacPosition
    let direction = pacmanState.PacState2.PacFacingDirection
    let mode      = pacmanState.PacState2.PacMode

    match mode with

        | PacAlive ->
            
            let struct (up, down, left, right) = KeysFrom keyStateGetter
            
            let directionImpliedByKeys  = KeyStatesToDirection up down left right direction
            let angleToCurrentDirection = AngleBetween directionImpliedByKeys direction

            let tile = TileIndexOf position

            let direction =
                match angleToCurrentDirection with
                    | ZeroAngle 
                    | AboutTurn180 -> directionImpliedByKeys

                    | AntiClockwiseTurn90
                    | ClockwiseTurn90 ->
                        match tile with
                            | None -> direction // disallow, not perfectly aligned
                            | Some (txi, tyi) ->
                                let i = tyi * mazeState.MazeTilesCountX + txi
                                if directionImpliedByKeys 
                                    |> IsDirectionAllowedBy mazeState.MazePlayersRails.[i] then
                                    directionImpliedByKeys
                                else
                                    direction // disallow, no exit in that direction.

            let eaten, scoreIncrement =
                match tile with
                    | None -> EatenNothing , 0u
                    | Some (txi, tyi) ->
                        let i = tyi * mazeState.MazeTilesCountX + txi

                        let tileType = mazeState.MazeTiles.[i]
                        if tileType = ((byte) TileIndex.Dot) then
                            (EatenDot i) , ScoreForEatingDot

                        else if tileType = ((byte) TileIndex.Pill1) then   // We don't store Pill2 in the matrix.
                            (EatenPowerPill i) , ScoreForEatingPowerPill

                        else
                            EatenNothing , 0u

            let position =  // TODO: issue of frame rate!

                let proposedPosition = 
                    position 
                        |> PointMovedByDelta (direction |> DirectionToMovementDeltaI32)
                        |> PointWrappedAtMazeEdges mazeState

                match tile with
                    | None -> proposedPosition // Can always allow movement when inbetween tiles
                    | Some (txi, tyi) ->
                        let i = tyi * mazeState.MazeTilesCountX + txi
                        if direction |> IsDirectionAllowedBy mazeState.MazePlayersRails.[i] then
                            proposedPosition
                        else
                            position // disallow, no exit in that direction.
            
            (eaten , position , direction , scoreIncrement)            


        | PacDyingUntil _
        | PacDead ->
            (EatenNothing , position , direction , 0u)


// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  Algorithms
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

// TODO: Use PointWrappedAtMazeEdges when determining who sees who?   (Tiny edge case improvement).


let IsTheSameGhostAs ghost otherGhost =
    ghost.GhostState2.GhostNumber = otherGhost.GhostState2.GhostNumber



let IsIntersectedByAnyOtherGhostTo selfGhost allGhosts corridorRect =

    allGhosts |> List.exists (fun otherGhost ->
        if selfGhost |> IsTheSameGhostAs otherGhost then
            false  // We do not "see" ourself down the corridors.
        else
            otherGhost.GhostPosition 
                |> TileBoundingRectangle
                |> RectangleIntersects corridorRect)



let IsStraightRail rail =

    System.Diagnostics.Debug.Assert (rail <> 0uy)   // Should never have empty rails.

    let horizontalRail = (MazeByteLeft ||| MazeByteRight)
    let verticalRail   = (MazeByteUp ||| MazeByteDown)
    (rail = horizontalRail)  ||  (rail = verticalRail)

    

let private EliminatingSuboptimalDirectionsForNormalGhost ghost mazeState tileXY pacRect allGhosts directionChoices =

    let corridorRect direction = 
        CorridorRectangle 
            mazeState.MazeTilesCountX
            mazeState.MazeTilesCountY
            mazeState.MazeTiles
            tileXY
            direction

    // TODO: We call corridorRect for 0-probability directions (which will never be chosen)
    //       and don't even read the rectangle.

    let corridorFacingLeft  = corridorRect FacingLeft
    let corridorFacingUp    = corridorRect FacingUp
    let corridorFacingRight = corridorRect FacingRight
    let corridorFacingDown  = corridorRect FacingDown

    let leftOnly  prob = { ProbLeft=prob ; ProbUp=0uy  ; ProbRight=0uy  ; ProbDown=0uy  }
    let upOnly    prob = { ProbLeft=0uy  ; ProbUp=prob ; ProbRight=0uy  ; ProbDown=0uy  }
    let rightOnly prob = { ProbLeft=0uy  ; ProbUp=0uy  ; ProbRight=prob ; ProbDown=0uy  }
    let downOnly  prob = { ProbLeft=0uy  ; ProbUp=0uy  ; ProbRight=0uy  ; ProbDown=prob }
    
    let lookForPacman prob corridorRect singleDirectionWithProbability acc =
        match acc with
            | Some _directionWherePacmanIsAlreadySeen -> acc
            | None ->
                if prob = 0uy then
                    None
                else if pacRect |> RectangleIntersects corridorRect then
                    Some (singleDirectionWithProbability prob)
                else
                    None

    let seenPac =
        None
            |> lookForPacman  directionChoices.ProbLeft   corridorFacingLeft   leftOnly  
            |> lookForPacman  directionChoices.ProbUp     corridorFacingUp     upOnly    
            |> lookForPacman  directionChoices.ProbRight  corridorFacingRight  rightOnly 
            |> lookForPacman  directionChoices.ProbDown   corridorFacingDown   downOnly  

    match seenPac with
        | Some directionTowardsPacman -> directionTowardsPacman
        | None ->

            let possiblyEliminated prob corridorRect direction =
                if prob = 0uy then
                    0uy
                else if corridorRect |> IsIntersectedByAnyOtherGhostTo ghost allGhosts then
                    0uy
                else
                    prob

            {
                ProbLeft  = possiblyEliminated  directionChoices.ProbLeft   corridorFacingLeft   FacingLeft
                ProbUp    = possiblyEliminated  directionChoices.ProbUp     corridorFacingUp     FacingUp
                ProbRight = possiblyEliminated  directionChoices.ProbRight  corridorFacingRight  FacingRight
                ProbDown  = possiblyEliminated  directionChoices.ProbDown   corridorFacingDown   FacingDown
            }



let private EliminatingSuboptimalDirectionsForEdibleGhost mazeState tileXY pacRect directionChoices =

    let possiblyEliminated probability direction =
        if probability = 0uy then
            0uy
        else
            let corridorRect = 
                CorridorRectangle 
                    mazeState.MazeTilesCountX
                    mazeState.MazeTilesCountY
                    mazeState.MazeTiles
                    tileXY
                    direction

            if corridorRect |> RectangleIntersects pacRect then
                0uy
            else
                probability

    {
        ProbLeft  = possiblyEliminated  directionChoices.ProbLeft   FacingLeft
        ProbUp    = possiblyEliminated  directionChoices.ProbUp     FacingUp
        ProbRight = possiblyEliminated  directionChoices.ProbRight  FacingRight
        ProbDown  = possiblyEliminated  directionChoices.ProbDown   FacingDown
    }



/// Returns true if no directions are available according to 
/// individual direction probabilities.
let NoDirectionsAvailable directionChoices = 
    (directionChoices.ProbLeft 
        ||| directionChoices.ProbUp 
        ||| directionChoices.ProbDown 
        ||| directionChoices.ProbRight) = 0uy



/// Returns a direction chosen from those which have non-zero probability.
/// The choice is pseudo-random, weighted on the probability values of the
/// direction choices.
let DirectionChosenRandomlyFrom directionChoices (XorShift32State(rand)) =

    let l = ((uint32) directionChoices.ProbLeft)
    let u = ((uint32) directionChoices.ProbUp)
    let d = ((uint32) directionChoices.ProbDown)
    let r = ((uint32) directionChoices.ProbRight)

    let probTotal = l + u + r + d

    System.Diagnostics.Debug.Assert (probTotal <> 0u)  // There must always be one direction.

    let n = rand % probTotal

    if l <> 0u && n < l then FacingLeft
    else if u <> 0u && n < (l + u) then FacingUp
    else if d <> 0u && n < (l + u + d) then FacingDown
    else if r <> 0u && n < (l + u + d + r) then FacingRight
    else failwith "It should never be the case there are *no* exits from a decision point!"



    
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  Ghost position advance
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let EliminatingDirectionChoicesGivenByBitmask bitmaskByte directionChoices =

    System.Diagnostics.Debug.Assert (bitmaskByte <> 0uy)   // Should never have empty directions mask.

    let maskedBy mask probValue =
        if mask=0uy then 0uy else probValue

    {
        ProbLeft  = directionChoices.ProbLeft  |> maskedBy (bitmaskByte &&& MazeByteLeft )
        ProbUp    = directionChoices.ProbUp    |> maskedBy (bitmaskByte &&& MazeByteUp   )
        ProbRight = directionChoices.ProbRight |> maskedBy (bitmaskByte &&& MazeByteRight)
        ProbDown  = directionChoices.ProbDown  |> maskedBy (bitmaskByte &&& MazeByteDown )
    }


let private DecideNewPositionAndDirectionFor 
    (ghost:GhostState) 
    mazeState 
    (allGhosts:GhostState list) 
    (pacman:PacmanState) 
    (rand:XorShift32State)
    gameTime =

    // NB:  Only called for GhostNormal and GhostEdibleUntil cases.

    let position  = ghost.GhostPosition
    let direction = ghost.GhostState2.GhostFacingDirection
    let tile      = TileIndexOf position
    let rails     = mazeState.MazeGhostRails

    let direction =
        match tile with

            | None -> 
                direction

            | Some (txi, tyi) ->  // Ghost precisely on the tile at (txi,tyi).  This is a decision point.
                let i = tyi * mazeState.MazeTilesCountX + txi
                let railsBitmask = rails.[i]
                let tileXY = { ptx=txi ; pty=tyi }

                if railsBitmask |> IsStraightRail then
                    direction

                else
                    let defaultDirectionChoices = 
                        ghost 
                            |> GhostDirectionChoiceProbabilities 
                            |> EliminatingDirectionChoicesGivenByBitmask railsBitmask

                    let directionChoices =

                        let pacRect = pacman.PacPosition |> TileBoundingRectangle

                        match ghost.GhostState2.GhostMode with
                            
                            | GhostNormal -> 
                                defaultDirectionChoices 
                                    |> EliminatingSuboptimalDirectionsForNormalGhost ghost mazeState tileXY pacRect allGhosts
                            
                            | GhostEdibleUntil _ -> 
                                defaultDirectionChoices 
                                    |> EliminatingSuboptimalDirectionsForEdibleGhost mazeState tileXY pacRect
                            
                            | _ -> failwith "Should not be deciding direction for ghost in this state"
                        
                        |> UpdateToValueWhen NoDirectionsAvailable defaultDirectionChoices

                    DirectionChosenRandomlyFrom directionChoices rand

    let position = 

        let potential =
            position 
                |> PointMovedByDelta (direction |> DirectionToMovementDeltaI32)
                |> PointWrappedAtMazeEdges mazeState

        match ghost.GhostState2.GhostMode with
            | GhostNormal -> potential
            | GhostEdibleUntil _ ->
                if gameTime |> PulseActiveAtRate 20.0F then potential else position  // TODO: better treatment.
            | _ -> failwith "Should not be deciding direction for ghost in this state"

    (position, direction)



let MovedTowardsHomePosition ghost =

    let delta = 
        ghost.GhostPosition 
            |> SimpleMovementDeltaI32ToGetTo ghost.GhostState2.GhostHomePosition

    let position = 
        ghost.GhostPosition 
            |> PointMovedByDelta delta

    (position , ghost.GhostState2.GhostFacingDirection)



let private AdvanceGhost mazeState allGhosts pacman ghost rand gameTime =

    let (position , direction) =
        match ghost |> GhostMode with
            | GhostNormal
            | GhostEdibleUntil _ -> 
                DecideNewPositionAndDirectionFor ghost mazeState allGhosts pacman rand gameTime

            | GhostReturningToBase ->
                ghost |> MovedTowardsHomePosition

            | GhostRegeneratingUntil _ ->
                // No movement while re-generating in the base.
                (ghost.GhostPosition , ghost.GhostState2.GhostFacingDirection) 

    {
        GhostPosition = position
        GhostState2 =
            {
                GhostNumber          = ghost.GhostState2.GhostNumber
                GhostHomePosition    = ghost.GhostState2.GhostHomePosition
                GhostMode            = ghost.GhostState2.GhostMode
                GhostFacingDirection = direction
                GhostDirectionChoiceProbabilities = ghost.GhostState2.GhostDirectionChoiceProbabilities
            }
    }


let private WithGhostMovement mazeState pacman rand gameTime allGhosts =

    let mutable rand = rand  // TODO: remove

    allGhosts |> List.map (fun ghost -> 
        rand <- rand |> XorShift32
        AdvanceGhost mazeState allGhosts pacman ghost rand gameTime)


// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  Post Life Loss Handling
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

/// Return pacmans position reset, for use after life loss.
let WithPacmanReset pacmanState =
    // TODO: If LivesLeft = 1 on entry then this should return None
    { 
        PacPosition = pacmanState.PacState2.PacHomePosition
        PacState2 =
            { 
                PacFacingDirection = FacingRight
                PacMode            = PacAlive
                LivesLeft          = pacmanState.PacState2.LivesLeft - 1
                PacHomePosition    = pacmanState.PacState2.PacHomePosition
            } 
    }


/// Return ghosts position reset, for use after pacman life loss.
let WithGhostReset ghostState =
    { 
        GhostPosition = ghostState.GhostState2.GhostHomePosition
        GhostState2 = 
            { 
                GhostMode            = GhostNormal
                GhostFacingDirection = FacingUp 
                GhostNumber          = ghostState.GhostState2.GhostNumber
                GhostHomePosition    = ghostState.GhostState2.GhostHomePosition
                GhostDirectionChoiceProbabilities = ghostState.GhostState2.GhostDirectionChoiceProbabilities
            } 
    }


/// Returns a model with the characters reset for use after a life loss.
let private WithCharactersReset model =
    {
        Random            = model.Random
        LevelNumber       = model.LevelNumber
        ScoreAndHiScore   = model.ScoreAndHiScore  
        MazeState         = model.MazeState        
        PacmanState       = model.PacmanState |> WithPacmanReset
        GhostsState       = model.GhostsState |> List.map WithGhostReset
        WhereToOnGameOver = model.WhereToOnGameOver
        WhereToOnAllEaten = model.WhereToOnAllEaten
    }





// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  Collisions PAC vs GHOSTS
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

/// State changes on pacman as a result of collision detection with ghosts
let WithStateChangesResultingFromCollisionWithGhosts ghostStateList gameTime pacmanState =   // TODO: Return indicator of new state, instead of new record

    match pacmanState.PacState2.PacMode with

        | PacAlive ->
            let pacmanRectangle = pacmanState |> PacCollisionRectangle
            pacmanState |> UpdateIf 
                (pacmanRectangle |> IntersectsNormalGhostsIn ghostStateList)
                (WithPacMode (PacDyingUntil (gameTime + PacmanDyingAnimationTime)))

        | PacDyingUntil t ->
            pacmanState |> UpdateIf (gameTime >= t) (WithPacMode PacDead)

        | PacDead -> 
            pacmanState



/// State changes on ghosts as a result of collision detection with pacman
let WithStateChangesResultingFromCollisionWithPacman pacmanPos ghosts =   // TODO: Return indicator of new state, instead of new record

    let pacmanRectangle = pacmanPos |> PacCollisionRectangle

    let isEdibleGhostOverlappingPacmanAt pacmanRectangle ghost =

        match ghost.GhostState2.GhostMode with
            | GhostNormal
            | GhostReturningToBase      
            | GhostRegeneratingUntil _  -> false
            | GhostEdibleUntil _ -> 
                ghost 
                    |> GhostCollisionRectangle
                    |> RectangleIntersects pacmanRectangle

    // Garbage optimisation:  Scan for intersections and calculate score for eaten ghosts.

    let score = 
        ghosts |> List.sumBy (fun ghost ->
            if ghost |> isEdibleGhostOverlappingPacmanAt pacmanRectangle then 
                ScoreForEatingGhost 
            else 
                NoScore)

    let ghosts = 
        ghosts |> 
            UpdateIf 
                (score > 0u)
                (List.map (UpdateWhen 
                                (isEdibleGhostOverlappingPacmanAt pacmanRectangle) 
                                (WithGhostMode GhostReturningToBase)))

    struct (ghosts , score)


// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  State update application
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

/// State update for pacman position and direction.
let WithPacManMovementStateChangesAppliedFrom position direction pacmanState =

    if direction = pacmanState.PacState2.PacFacingDirection then
        {
            PacState2 = pacmanState.PacState2  // unchanged
            PacPosition = position
        }
    else
        {
            PacState2 =
                {
                    PacMode            = pacmanState.PacState2.PacMode
                    PacFacingDirection = direction
                    LivesLeft          = pacmanState.PacState2.LivesLeft
                    PacHomePosition    = pacmanState.PacState2.PacHomePosition
                }
            PacPosition = position
        }


/// State update for the maze as pac man eats things.
let private WithDotsRemovedFromArrayWhere eaten mazeState =

    match eaten with
        | EatenNothing -> mazeState
        | EatenDot tileIndex
        | EatenPowerPill tileIndex ->
            mazeState.MazeTiles.[tileIndex] <- ((byte) TileIndex.Blank)  // mutable
            mazeState


/// State update for ghosts if pacman eats a power pill
let private WithEdibleGhostsIfPowerPill eaten gameTime ghostStateList =

    match eaten with
        | EatenNothing 
        | EatenDot _ -> ghostStateList
        | EatenPowerPill _ ->
            ghostStateList |> List.map (fun ghost ->
                match ghost |> GhostMode with
                    | GhostNormal
                    | GhostEdibleUntil _ -> ghost |> WithGhostMode (GhostEdibleUntil (gameTime + PowerPillTime))
                    | GhostReturningToBase
                    | GhostRegeneratingUntil _ -> ghost  // This ghost does not become edible.
            )


/// State update for ghosts based on timeouts.
let private WithReturnToNormalityIfTimeOutAt gameTime ghostStateList =
    ghostStateList |> List.map (fun ghost ->
        match ghost |> GhostMode with
            | GhostNormal ->
                ghost

            | GhostEdibleUntil t
            | GhostRegeneratingUntil t -> 
                ghost |> UpdateIf (gameTime >= t) (WithGhostMode GhostNormal)

            | GhostReturningToBase ->
                ghost |> UpdateWhen IsAtHomePosition (WithGhostMode (GhostRegeneratingUntil (gameTime + RegenerationTime)))
    )





// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
//  Screen state advance on frame
// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let IsSomethingPacManNeedsToEat (tile:byte) =
    (tile = ((byte)TileIndex.Pill1)) || (tile = ((byte)TileIndex.Dot))   // NB: Pill2 is never encoded in the maze itself.



let private IsAllEaten maze =
    not  (maze.MazeTiles |> Array.exists (fun tile -> tile |> IsSomethingPacManNeedsToEat))



let LifeIsOver pacman =
    match pacman.PacState2.PacMode with
        | PacDead -> true
        | _ -> false



let private NextPacmanScreenState gameState keyStateGetter gameTime elapsed =

    // Unpack

    let model = ModelFrom gameState

    let {
            Random            = rand
            ScoreAndHiScore   = scoreAndHiScore
            MazeState         = mazeState
            PacmanState       = pacmanState
            GhostsState       = ghostStateList
            WhereToOnGameOver = _
            WhereToOnAllEaten = _
        }
            = model

    let eaten , position , direction , scoreIncrement =
        AdvancePacMan keyStateGetter mazeState pacmanState

    let scoreAndHiScore =
        scoreAndHiScore |> ScoreIncrementedBy scoreIncrement

    let pacmanState =
        pacmanState |> WithPacManMovementStateChangesAppliedFrom position direction

    let mazeState =
        mazeState |> WithDotsRemovedFromArrayWhere eaten

    let ghostStateList =
        ghostStateList 
            |> WithReturnToNormalityIfTimeOutAt gameTime
            |> WithEdibleGhostsIfPowerPill eaten gameTime
            |> WithGhostMovement mazeState pacmanState rand gameTime

    let pacmanState =
        pacmanState |> WithStateChangesResultingFromCollisionWithGhosts ghostStateList gameTime

    let struct (ghostStateList , scoreIncrement) =
        ghostStateList |> WithStateChangesResultingFromCollisionWithPacman pacmanState

    let scoreAndHiScore =
        scoreAndHiScore |> ScoreIncrementedBy scoreIncrement

    // Repack

    let model =
        {
            Random            = model.Random |> XorShift32
            LevelNumber       = model.LevelNumber
            ScoreAndHiScore   = scoreAndHiScore
            MazeState         = mazeState
            PacmanState       = pacmanState
            GhostsState       = ghostStateList
            WhereToOnGameOver = model.WhereToOnGameOver
            WhereToOnAllEaten = model.WhereToOnAllEaten
        }        

    // Decide next gameState

    if pacmanState |> LifeIsOver then
        if pacmanState.PacState2.LivesLeft > 1 then
            let whereToAfterIntermission =
                fun _gameTime -> model |> WithCharactersReset |> ReplacesModelIn gameState
            WithLifeLossIntermissionCard whereToAfterIntermission gameTime  // TODO: There is something bad about this parameter order, that we can't use |>
        else
            model.WhereToOnGameOver scoreAndHiScore
    
    else if mazeState |> IsAllEaten then
        model.WhereToOnAllEaten model.LevelNumber scoreAndHiScore gameTime  // TODO: Maze flash - but could that be done with a clever external filter?
    
    else 
        gameState |> WithUpdatedModel model

// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 

let NewPacmanScreen levelNumber whereToOnAllEaten whereToOnGameOver scoreAndHiScore =

    let numberOfMazes = 
        AllPacmanMazes.Length

    let unpackedMaze = 
        AllPacmanMazes.[levelNumber % numberOfMazes] |> TextMazeDefinitionUnpacked

    let ghostDirectionProbabilities = // TODO sort out
        {
            ProbAhead   = 70uy
            ProbTurn90  = 20uy
            ProbTurn180 = 10uy
        }

    let ghostProbArray = // TODO: sharing the same for all ghosts right now
        ghostDirectionProbabilities |> CalculateMemoizedDirectionProbabilities

    let pacModel =
        {
            Random            = XorShift32State 0x33033u
            LevelNumber       = levelNumber
            ScoreAndHiScore   = scoreAndHiScore
            MazeState         = unpackedMaze.UnpackedMazeState
            WhereToOnGameOver = whereToOnGameOver
            WhereToOnAllEaten = whereToOnAllEaten
            
            // TODO: sort out
            PacmanState =
                { 
                    PacPosition = unpackedMaze.UnpackedPacmanPosition
                    PacState2 = 
                        { 
                            PacFacingDirection = FacingRight
                            PacMode = PacAlive
                            LivesLeft = InitialLives
                            PacHomePosition = unpackedMaze.UnpackedPacmanPosition
                        } 
                }

            GhostsState = unpackedMaze.UnpackedGhostPositions |> List.mapi (fun i ghostPos -> 
                { 
                    GhostPosition = ghostPos
                    GhostState2 = 
                        { 
                            GhostNumber = GhostNumber(i)
                            GhostMode = GhostNormal
                            GhostHomePosition = ghostPos
                            GhostFacingDirection = FacingUp 
                            GhostDirectionChoiceProbabilities = ghostProbArray
                        } 
                })
        }

    NewGameState NextPacmanScreenState RenderPacmanScreen pacModel

