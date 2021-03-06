﻿module BeachBackgroundRenderer

open Geometry
open ResourceIDs
open DrawingFunctions
open Time
open StaticResourceAccess



let RenderBeachBackground render (gameTime:GameTime) =

    // TODO: We need repeat-tile-rendering support for this!
    // TODO: constants throughout this routine

    let mutable x = (((int (gameTime * 5.0)) % 89) - 89) |> AsIntEpx

    let imgCliffs = CliffsTileImageID |> ImageFromID

    for i in 1..5 do
        Image1to1 render x 0<epx> imgCliffs
        x <- x + 89<epx>


