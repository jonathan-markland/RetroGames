module WebGameFramework

open Fable.Core
open Browser.Dom

open StaticResourceSetup
open KeyboardForFramework

open Time
open Geometry
open DrawingShapes
open ImagesAndFonts

open Input
open ScreenHandler



// [ ] TODO: If ever the onLoad fails on the javascript side, the continuation will never be called, so the game won't start.


        // The start command is:    npm start


type JavascriptGraphicResources =
    {
        Fonts  : Font[]
        Images : Image[]
    }



// ------------------------------------------------------------------------------------------------------------
//  Fable to Javascript interfacing
// ------------------------------------------------------------------------------------------------------------

[<Emit("console.log($0)")>]
let private ConsoleLog (messageText:string) : unit = jsNative



/// Javascript alert() function
[<Emit("alert($0)")>]
let private Alert (messageText:string) : unit = jsNative



/// A supplementary Javascript function that we made.  TODO: It may not be necessary to even have this in Javascript!
[<Emit("loadImageThenDo($0, $1, $2)")>]
let private LoadImageThenDo
    (htmlImageElement:obj)
    (needsMagentaColourKey:bool)
    (onCompletionOfLoad:obj -> unit) : unit = jsNative



[<Emit("$0.drawImage($1, $2, $3)")>]
let private JsDrawImage 
    (context2d:Browser.Types.CanvasRenderingContext2D)
    (htmlImageObject:obj)
    (x:int)
    (y:int) = jsNative

let inline private DrawImage context2d (HostImageRef(htmlImageObject)) x y =
    JsDrawImage context2d htmlImageObject x y


[<Emit("$0.drawImage($1, $2, $3, $4, $5, $6, $7, $8, $9)")>]
let private JsDrawSubImage 
    (context2d:Browser.Types.CanvasRenderingContext2D) // $0
    (htmlImageObject:obj)                              // $1
    (srcleft:int)                                      // $2
    (srctop:int)                                       // $3
    (srcwidth:int)                                     // $4 
    (srcheight:int)                                    // $5 
    (dstleft:int)                                      // $6
    (dsttop:int)                                       // $7
    (dstwidth:int)                                     // $8 
    (dstheight:int) = jsNative                         // $9 

let inline private DrawSubImage context2d (HostImageRef(htmlImageObject)) srcleft srctop srcwidth srcheight dstleft dsttop dstwidth dstheight =
    if srcwidth > 0 && srcheight > 0 && dstwidth > 0 && dstheight > 0 then // Avoid firefox exception
        JsDrawSubImage context2d htmlImageObject srcleft srctop srcwidth srcheight dstleft dsttop dstwidth dstheight


[<Emit("$0.fillStyle=$5 ; $0.fillRect($1,$2,$3,$4)")>]
let private JsDrawFilledRectangle (context2d:Browser.Types.CanvasRenderingContext2D) (x:int) (y:int) (w:int) (h:int) (colour:string) = jsNative
    
let inline private DrawFilledRectangle context2d x y w h (colouru:uint32) =
    let colourStr = "#" + colouru.ToString("x6")
    JsDrawFilledRectangle context2d x y w h colourStr



// ------------------------------------------------------------------------------------------------------------
//  Load resources then start game
// ------------------------------------------------------------------------------------------------------------

let private LoadFileListThenDo fileNameObtainer needsMagentaObtainer widthGetter heightGetter continuation resourceList =

    let htmlImageElementResizeArrayForFonts = new ResizeArray<Image>(resourceList |> List.length)

    let rec recurse resourceRecordList fileNameObtainer needsMagentaObtainer =

        match resourceRecordList with
            | [] -> 
                continuation (htmlImageElementResizeArrayForFonts.ToArray())

            | resourceRecord::tail ->
                let fileName = resourceRecord |> fileNameObtainer
                let needsMagentaColourKeying = resourceRecord |> needsMagentaObtainer
                let w = resourceRecord |> widthGetter
                let h = resourceRecord |> heightGetter

                LoadImageThenDo fileName needsMagentaColourKeying (fun htmlImageElement ->

                    let imgWithHostObject =
                        {
                            ImageMetadata = 
                                {
                                    ImageFileName  = fileName
                                    ImageTransparency = if needsMagentaColourKeying then MagentaColourKeyImage else OpaqueImage
                                    ImageWidth     = w
                                    ImageHeight    = h
                                }
                            HostImageRef = HostImageRef(htmlImageElement)
                        }

                    htmlImageElementResizeArrayForFonts.Add(imgWithHostObject)

                    recurse tail fileNameObtainer needsMagentaObtainer
                )

    recurse resourceList fileNameObtainer needsMagentaObtainer

    // We never get here because the | [] -> match case is the final "what to do next" (need continuation-pass)

// ------------------------------------------------------------------------------------------------------------

// TODO: This should not need to be private?
let LoadResourceFilesThenDo resourceImages fontResourceImages afterAllLoaded =

    let imageFileNameGetter metadata =
        metadata.ImageFileName

    let imageIsColourKeyed metadata =
        match metadata.ImageTransparency with 
            | OpaqueImage -> false
            | MagentaColourKeyImage -> true

    let imageWidthGetter  metadata = metadata.ImageWidth
    let imageHeightGetter metadata = metadata.ImageHeight


    fontResourceImages |> LoadFileListThenDo imageFileNameGetter imageIsColourKeyed imageWidthGetter imageHeightGetter
        (fun arrayOfLoadedFontImages ->

            let arrayOfLoadedFonts = 
                arrayOfLoadedFontImages |> Array.map BasicFont

            resourceImages |> LoadFileListThenDo imageFileNameGetter imageIsColourKeyed imageWidthGetter imageHeightGetter
                (fun arrayOfLoadedImages ->
                    afterAllLoaded arrayOfLoadedImages arrayOfLoadedFonts)
        )

    // NB: We never get here (continuations called).

// ------------------------------------------------------------------------------------------------------------

let private RenderToWebCanvas (context2d:Browser.Types.CanvasRenderingContext2D) drawingCommand =

    match drawingCommand with

        | DrawImageWithTopLeftAtInt(left, top, imageVisual) ->
            DrawImage 
                context2d imageVisual.HostImageRef 
                (left |> IntEpxToInt) (top |> IntEpxToInt)

        | DrawStretchedImageWithTopLeftAt(left, top, imageVisual, width, height) ->
            let (w,h) = (imageVisual.ImageMetadata.ImageWidth , imageVisual.ImageMetadata.ImageHeight)
            DrawSubImage 
                context2d imageVisual.HostImageRef
                0 0 (w |> IntEpxToInt) (h |> IntEpxToInt) 
                (left |> FloatEpxToInt) (top |> FloatEpxToInt) (width |> IntEpxToInt) (height |> IntEpxToInt)

        | DrawSubImageStretchedToTarget(srcleft, srctop, srcwidth, srcheight, dstleft, dsttop, dstwidth, dstheight, imageVisual) ->
            DrawSubImage 
                context2d imageVisual.HostImageRef
                srcleft srctop srcwidth srcheight 
                (dstleft |> FloatEpxToInt) (dsttop |> FloatEpxToInt) (dstwidth |> IntEpxToInt) (dstheight |> IntEpxToInt)

        | DrawFilledRectangle(left, top, width, height, SolidColour colour) ->
            let width  = width  |> IntEpxToInt
            let height = height |> IntEpxToInt
            let left   = left   |> IntEpxToInt
            let top    = top    |> IntEpxToInt
            DrawFilledRectangle 
                context2d 
                left top width height 
                colour

// ------------------------------------------------------------------------------------------------------------

let FrameworkWebMain
    listOfKeysNeeded
    gameStaticDataConstructor
    gameGlobalStateConstructor
    gameplayStartConstructor
    arrayOfLoadedImages
    arrayOfLoadedFonts =

    SetStaticImageAndFontResourceArrays arrayOfLoadedImages arrayOfLoadedFonts

    let canvas = document.getElementById("gameScreen") :?> Browser.Types.HTMLCanvasElement
    let context2d = canvas.getContext("2d") :?> Browser.Types.CanvasRenderingContext2D
   
    match gameStaticDataConstructor () with
        | Error msg -> ConsoleLog msg
        | Ok gameStaticData ->

            let gameGlobalState =
                match gameGlobalStateConstructor () with
                    | Error msg -> failwith msg
                    | Ok globals -> globals

            let gameTime = 0.0F<seconds>
            let frameElapsedTime = 0.02F<seconds>

            let gameState : ErasedGameState<'StaticGameResources> =
                gameplayStartConstructor gameStaticData gameGlobalState gameTime

            let renderFunction = RenderToWebCanvas context2d

            let toKeyTuple (WebBrowserKeyCode k) =
                (k, WebBrowserKeyCode k)

            let toKeyTuples lst =
                lst |> List.map toKeyTuple
            
            let mutableKeyStateStore =
                NewMutableKeyStateStore
                    80 // P
                    (listOfKeysNeeded |> toKeyTuples)

            let registerKeyHandler eventName handlerFunc =
                document.addEventListener(
                    eventName, 
                    fun e -> 
                        let ke: Browser.Types.KeyboardEvent = downcast e
                        if handlerFunc mutableKeyStateStore ((int) ke.keyCode) then e.preventDefault())

            registerKeyHandler "keydown" HandleKeyDownEvent
            registerKeyHandler "keyup"   HandleKeyUpEvent

            document.getElementById("loaderScreen").classList.add("hidden")
            document.getElementById("gameScreen").classList.remove("hidden")

            let keyStateGetter = 
                LiveKeyStateFrom mutableKeyStateStore

            let rec mainLoop (gameState : ErasedGameState<'StaticGameResources>) tickCount () =

                let tickCount = tickCount + 1u
                
                let gameTime = 
                    (float32 tickCount) / 50.0F |> InSeconds
                
                gameState.Draw renderFunction gameTime

                let nextGameState = 
                    gameState.Frame 
                        gameStaticData 
                        keyStateGetter 
                        gameTime 
                        frameElapsedTime 

                ClearKeyJustPressedFlags mutableKeyStateStore

                window.setTimeout((mainLoop nextGameState tickCount), 20) |> ignore

            mainLoop gameState 0u ()
    
