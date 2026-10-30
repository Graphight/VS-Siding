using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class SidingWallEntityTests
{
    internal static JsonObject Dict(string json) => new(JToken.Parse(json));

    private static readonly JsonObject NoElementFinishes = Dict("""{ "daub": {} }""");

    private static readonly JsonObject PlankFinishes = Dict("""
    {
        "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" } }
    }
    """);

    [Fact]
    public void AFramingNamesItsGroupsOrFallsBackToThePlainOnes()
    {
        var framings = Dict("""{ "oak": {}, "sticks": { "Elements": "poles" } }""");

        Assert.Equal(
            new[] { "framing", "poles", "framing", "poles", "poles2", "framing" },
            new (string?, int)[] { ("oak", 0), ("sticks", 0), (null, 0), ("sticks", 2), ("sticks", 3), ("oak", 3) }
                .Select(c => SidingWallEntity.FrameElements(framings, c.Item1, c.Item2)));
    }

    // A stick wall of packed dirt once took the pole variant's parity as its dirt texture's variant too.
    [Fact]
    public void ACellKeepsTheWholeHashWhileAnyLayersTextureVaries()
    {
        var attributes = Dict("""
        {
            "Framings": { "oak": { "Texture": "planks/oak*" }, "aged": { "Texture": "planks/aged1" }, "sticks": { "Texture": "bark/oak", "Elements": "poles" } },
            "Infills": { "wattle": { "Texture": "wattle" }, "packeddirt": { "Texture": "packeddirt1*" } },
            "Finishes": { "daub": { "Texture": "daub1" }, "planks": { "Texture": "planks/oak*" } }
        }
        """);
        (string? Framing, string? Deck, string? Infill, string? Front)[] cells =
        [
            ("aged", null, "wattle", "daub"),
            ("sticks", null, "wattle", "daub"),
            ("sticks", null, "packeddirt", null),
            ("sticks", null, "wattle", "planks"),
            ("sticks", "oak", null, null),
            ("aged", "sticks", null, null),
            ("sticks", "aged", null, null),
            ("aged", null, "packeddirt", null),
            ("oak", null, null, null),
            (null, null, null, null),
        ];

        Assert.Equal(
            new[] { 0, 1, 77, 77, 77, 1, 1, 77, 77, 0 },
            cells.Select(c => SidingWallEntity.CellAlternate(attributes, 77, c.Framing, c.Deck, [c.Infill, null], [c.Front, null])));
    }

    // Three stacked infill boxes share a face at each seam; for glass that blends twice and draws
    // a bright line where a cross-beam would be. Glazing must take the single full-cell pane
    // instead, in every join state, and the pane must still count as infill for the render split.
    [Fact]
    public void GlazingTakesOneFullCellPaneInsteadOfTheFillerStack()
    {
        var joins = new[] { (false, false, false, false), (true, false, false, false), (true, true, false, false) };

        Assert.Equal(
            joins.ToDictionary(j => j, _ => new[] { "infill-pane" }),
            joins.ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "wall", "oak", "glass", null, null, null, NoElementFinishes, j, glazed: true)
                .Where(SidingWallEntity.IsInfillElement).ToArray()));

        // Opaque infill is untouched: it still gets the slab plus a filler per merged side.
        Assert.Equal(
            new[] { "infill", "infill-top", "infill-bottom" },
            SidingWallEntity.SelectiveElements(
                "wall", "oak", "wattle", null, null, null, NoElementFinishes, (true, true, false, false), glazed: false)
                .Where(SidingWallEntity.IsInfillElement).ToArray());
    }

    // Glazing splits the mesh in two by this predicate, so a name landing on the wrong side
    // renders the frame see-through or the glass solid - neither of which throws.
    [Fact]
    public void OnlyInfillElementsGoInTheTransparentHalf()
    {
        string[] elements = new[] { (false, false, false, false), (false, false, true, false), (false, false, false, true) }
            .SelectMany(j => SidingWallEntity.SelectiveElements(
                "wall", "oak", "glass", null, null, null, NoElementFinishes, j, glazed: true))
            .Distinct().ToArray();

        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["glazing-left"] = false,
                ["glazing-right"] = false,
                ["glazing-top"] = false,
                ["glazing-bottom"] = false,
                ["infill-pane"] = true,
                ["glazing-top-left"] = false,
                ["glazing-bottom-left"] = false,
                ["glazing-top-right"] = false,
                ["glazing-bottom-right"] = false,
            },
            elements.ToDictionary(name => name, SidingWallEntity.IsInfillElement));
    }

    // Merging belongs to glazing, not to a layout: a glazed cell drops the post or plate against
    // every glazed neighbour, so a run of them is one sheet with framing only round the outside.
    [Fact]
    public void GlazedRunDropsEveryMemberItMergesAgainst()
    {
        var joins = new (bool above, bool below, bool left, bool right)[]
        {
            (false, false, false, false),
            (false, false, true, false),
            (true, false, true, false),
            (true, false, false, true),
            (true, true, true, true),
        };

        Assert.Equal(
            new Dictionary<(bool, bool, bool, bool), string[]>
            {
                [(false, false, false, false)] = ["glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane"],
                // Left merged: its stile is gone, so each rail ends on a filler in that corner.
                [(false, false, true, false)] = ["glazing-right", "glazing-top", "glazing-top-left", "glazing-bottom", "glazing-bottom-left", "infill-pane"],
                // Left and above merged: the top rail is gone too, so only the bottom corner needs a filler.
                [(true, false, true, false)] = ["glazing-right", "glazing-bottom", "glazing-bottom-left", "infill-pane"],
                [(true, false, false, true)] = ["glazing-left", "glazing-bottom", "glazing-bottom-right", "infill-pane"],
                // Surrounded by glazing: nothing but the pane, so the run reads as one sheet.
                [(true, true, true, true)] = ["infill-pane"],
            },
            joins.ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "wall", "oak", "glass", null, null, null, NoElementFinishes, j, glazed: true)));
    }

    // A glazed wall's finish is drawn as a rim in place of its group, one piece for each part the frame
    // draws, so the border follows the frame wherever glazing merges.
    [Fact]
    public void GlazedWallDrawsEachFacesFinishAsARimOfTheFramesParts()
    {
        var finishes = Dict("""{ "daub": {}, "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" } } }""");
        var joins = new (bool above, bool below, bool left, bool right)[]
        {
            (false, false, false, false),
            (false, false, true, false),
            (true, false, true, false),
            (true, true, true, true),
        };

        Assert.Equal(
            new Dictionary<(bool, bool, bool, bool), string[]>
            {
                [(false, false, false, false)] =
                [
                    "front-weatherboard-rim-left", "front-weatherboard-rim-right", "front-weatherboard-rim-top", "front-weatherboard-rim-bottom",
                    "glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane",
                    "back-rim-left", "back-rim-right", "back-rim-top", "back-rim-bottom",
                ],
                [(false, false, true, false)] =
                [
                    "front-weatherboard-rim-right", "front-weatherboard-rim-top", "front-weatherboard-rim-top-left",
                    "front-weatherboard-rim-bottom", "front-weatherboard-rim-bottom-left",
                    "glazing-right", "glazing-top", "glazing-top-left", "glazing-bottom", "glazing-bottom-left", "infill-pane",
                    "back-rim-right", "back-rim-top", "back-rim-top-left", "back-rim-bottom", "back-rim-bottom-left",
                ],
                [(true, false, true, false)] =
                [
                    "front-weatherboard-rim-right", "front-weatherboard-rim-bottom", "front-weatherboard-rim-bottom-left",
                    "glazing-right", "glazing-bottom", "glazing-bottom-left", "infill-pane",
                    "back-rim-right", "back-rim-bottom", "back-rim-bottom-left",
                ],
                // Surrounded by glazing: nothing but the pane, finish or not.
                [(true, true, true, true)] = ["infill-pane"],
            },
            joins.ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "wall", "oak", "glass", "planks", null, "daub", finishes, j, glazed: true)));
    }

    // A glazed cornerout's corner post never drops and each leg's end post is named like a wall's stile, with a
    // rail per leg between them, and the leg's filler in each rail's corner in place of an end post that has dropped.
    // An opaque cornerout has no end posts, so it draws only its corner post and rails.
    [Fact]
    public void GlazedCorneroutKeepsItsCornerPostAndNamesItsEndPosts()
    {
        var joins = new (bool above, bool below, bool left, bool right)[]
        {
            (false, false, false, false),
            (false, false, true, true),
            (true, true, false, false),
        };

        Assert.Equal(
            new Dictionary<(bool, bool, bool, bool), string[]>
            {
                [(false, false, false, false)] = ["glazing", "glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane"],
                [(false, false, true, true)] =
                [
                    "glazing", "glazing-top", "glazing-top-left", "glazing-top-right",
                    "glazing-bottom", "glazing-bottom-left", "glazing-bottom-right", "infill-pane",
                ],
                [(true, true, false, false)] = ["glazing", "glazing-left", "glazing-right", "infill-pane"],
            },
            joins.ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "cornerout", "oak", "glass", null, null, null, NoElementFinishes, j, glazed: true)));
        Assert.Equal(
            ["framing", "framing-top", "framing-bottom", "infill"],
            SidingWallEntity.SelectiveElements(
                "cornerout", "oak", "wattle", null, null, null, NoElementFinishes, (false, false, false, false), glazed: false));
    }

    // Each end post drops on its own join: left is leg 2's, right is leg 1's, and the finish rims follow the legs.
    [Fact]
    public void AGlazedCorneroutDropsEachEndPostOnItsOwnJoin()
    {
        var finishes = Dict("""{ "daub": {} }""");

        Assert.Equal(
            new string[][]
            {
                [
                    "front-rim", "front-rim-right", "front-rim-top", "front-rim-bottom",
                    "secondfront-rim", "secondfront-rim-top", "secondfront-rim-top-left", "secondfront-rim-bottom", "secondfront-rim-bottom-left",
                    "glazing", "glazing-right", "glazing-top", "glazing-top-left", "glazing-bottom", "glazing-bottom-left", "infill-pane",
                ],
                [
                    "front-rim", "front-rim-top", "front-rim-top-right", "front-rim-bottom", "front-rim-bottom-right",
                    "secondfront-rim", "secondfront-rim-left", "secondfront-rim-top", "secondfront-rim-bottom",
                    "glazing", "glazing-left", "glazing-top", "glazing-top-right", "glazing-bottom", "glazing-bottom-right", "infill-pane",
                ],
            },
            new[]
            {
                SidingWallEntity.SelectiveElements("cornerout", "oak", "glass", "daub", "daub", null, finishes, (false, false, true, false), glazed: true),
                SidingWallEntity.SelectiveElements("cornerout", "oak", "glass", "daub", "daub", null, finishes, (false, false, false, true), glazed: true),
            });
    }

    // A glazed corner's finishes are rims too, on both legs: the corner post's piece always, the end post's piece
    // (or the fillers' once it drops), and the pieces over and under the opening for as long as the rails are
    // drawn. The front rims leg 1, whose end post is its right; the second front leg 2, whose is its left; the
    // back both.
    [Fact]
    public void GlazedCorneroutDrawsEachFacesFinishAsARimOfTheFramesParts()
    {
        var finishes = Dict("""{ "daub": {}, "planks": { "Elements": { "front": "front-weatherboard", "back": "back-boards" } } }""");
        var joins = new (bool above, bool below, bool left, bool right)[]
        {
            (false, false, false, false),
            (false, false, true, true),
            (true, false, false, false),
            (true, true, false, false),
        };

        Assert.Equal(
            new Dictionary<(bool, bool, bool, bool), string[]>
            {
                [(false, false, false, false)] =
                [
                    "front-weatherboard-rim", "front-weatherboard-rim-right", "front-weatherboard-rim-top", "front-weatherboard-rim-bottom",
                    "secondfront-rim", "secondfront-rim-left", "secondfront-rim-top", "secondfront-rim-bottom",
                    "glazing", "glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane",
                    "back-boards-rim", "back-boards-rim-left", "back-boards-rim-right", "back-boards-rim-top", "back-boards-rim-bottom",
                ],
                [(false, false, true, true)] =
                [
                    "front-weatherboard-rim", "front-weatherboard-rim-top", "front-weatherboard-rim-top-right",
                    "front-weatherboard-rim-bottom", "front-weatherboard-rim-bottom-right",
                    "secondfront-rim", "secondfront-rim-top", "secondfront-rim-top-left",
                    "secondfront-rim-bottom", "secondfront-rim-bottom-left",
                    "glazing", "glazing-top", "glazing-top-left", "glazing-top-right",
                    "glazing-bottom", "glazing-bottom-left", "glazing-bottom-right", "infill-pane",
                    "back-boards-rim", "back-boards-rim-top", "back-boards-rim-top-left", "back-boards-rim-top-right",
                    "back-boards-rim-bottom", "back-boards-rim-bottom-left", "back-boards-rim-bottom-right",
                ],
                [(true, false, false, false)] =
                [
                    "front-weatherboard-rim", "front-weatherboard-rim-right", "front-weatherboard-rim-bottom",
                    "secondfront-rim", "secondfront-rim-left", "secondfront-rim-bottom",
                    "glazing", "glazing-left", "glazing-right", "glazing-bottom", "infill-pane",
                    "back-boards-rim", "back-boards-rim-left", "back-boards-rim-right", "back-boards-rim-bottom",
                ],
                [(true, true, false, false)] =
                [
                    "front-weatherboard-rim", "front-weatherboard-rim-right", "secondfront-rim", "secondfront-rim-left",
                    "glazing", "glazing-left", "glazing-right", "infill-pane",
                    "back-boards-rim", "back-boards-rim-left", "back-boards-rim-right",
                ],
            },
            joins.ToDictionary(j => j, j => SidingWallEntity.SelectiveElements(
                "cornerout", "oak", "glass", "planks", "daub", "planks", finishes, j, glazed: true)));
    }

    // The top of the room side's rim lies in a deck's height. Under a deck it is left out, fillers and all,
    // and the deck's ledge is drawn there instead; the front's rim and the frame are untouched.
    [Fact]
    public void ADeckedGlazedCellsRoomSideRimLeavesItsTopToTheLedge()
    {
        var finishes = Dict("""{ "daub": {} }""");

        Assert.Equal(
            new string[][]
            {
                [
                    "front-rim-left", "front-rim-right", "front-rim-top", "front-rim-bottom",
                    "glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane",
                    "back-rim-left", "back-rim-right", "back-rim-bottom",
                ],
                [
                    "front-rim-right", "front-rim-top", "front-rim-top-left", "front-rim-bottom", "front-rim-bottom-left",
                    "glazing-right", "glazing-top", "glazing-top-left", "glazing-bottom", "glazing-bottom-left", "infill-pane",
                    "back-rim-right", "back-rim-bottom", "back-rim-bottom-left",
                ],
                [
                    "front-rim", "front-rim-right", "front-rim-top", "front-rim-bottom",
                    "glazing", "glazing-left", "glazing-right", "glazing-top", "glazing-bottom", "infill-pane",
                    "back-rim", "back-rim-left", "back-rim-right", "back-rim-bottom",
                ],
                ["framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back"],
            },
            new[]
            {
                SidingWallEntity.SelectiveElements("wall", "oak", "glass", "daub", null, "daub", finishes, (false, false, false, false), glazed: true, decked: true),
                SidingWallEntity.SelectiveElements("wall", "oak", "glass", "daub", null, "daub", finishes, (false, false, true, false), glazed: true, decked: true),
                SidingWallEntity.SelectiveElements("cornerout", "oak", "glass", "daub", null, "daub", finishes, (false, false, false, false), glazed: true, decked: true),
                SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, "daub", finishes, (false, false, false, false), glazed: false, decked: true),
            });
    }

    // A cell merged on both sides of a corner frames that corner only where the cell diagonally across carries
    // no glazing on: an L's inside corner is framed, and the middle of four merged cells is left clear.
    [Fact]
    public void AMergedCornerIsFramedOnlyWhereTheCellDiagonallyAcrossIsNoGlazing()
    {
        Assert.Equal(
            new string[][]
            {
                [],
                ["-top-left"],
                [],
                ["-top-left", "-top-right", "-bottom-left", "-bottom-right"],
                ["-top-right", "-bottom-left", "-bottom-right"],
            },
            new[]
            {
                SidingWallBlock.InnerCorners((true, false, false, false), (_, _) => false),
                SidingWallBlock.InnerCorners((true, false, true, false), (_, _) => false),
                SidingWallBlock.InnerCorners((true, false, true, false), (_, _) => true),
                SidingWallBlock.InnerCorners((true, true, true, true), (_, _) => false),
                SidingWallBlock.InnerCorners((true, true, true, true), (rail, stile) => rail + stile == "-top-left"),
            });
    }

    // An inside corner draws the frame's filler alone, and each finish's rim on it, on a wall, a floor and
    // a deck; an opaque wall has no fillers to draw.
    [Fact]
    public void AnInsideCornerDrawsItsFillerAndEachFinishesRimOnIt()
    {
        var finishes = Dict("""{ "daub": {} }""");
        var joins = (above: true, below: false, left: true, right: false);
        string[] innerCorners = ["-top-left"];

        Assert.Equal(
            new string[][]
            {
                [
                    "front-rim-right", "front-rim-bottom", "front-rim-bottom-left", "front-rim-top-left",
                    "glazing-right", "glazing-bottom", "glazing-bottom-left", "glazing-top-left", "infill-pane",
                    "back-rim-right", "back-rim-bottom", "back-rim-bottom-left", "back-rim-top-left",
                ],
                [
                    "front-rim-right", "front-rim-bottom", "front-rim-bottom-left", "front-rim-top-left",
                    "glazing-right", "glazing-bottom", "glazing-bottom-left", "glazing-top-left", "infill-pane",
                ],
                [
                    "deck-west-glazing-right", "deck-west-glazing-bottom", "deck-west-glazing-bottom-left", "deck-west-glazing-top-left",
                    "deck-west-infill-pane",
                ],
                ["glazing", "glazing-right", "glazing-bottom", "glazing-bottom-left", "glazing-top-left", "infill-pane"],
                ["framing-right", "framing-bottom", "infill", "infill-top"],
            },
            new[]
            {
                SidingWallEntity.SelectiveElements("wall", "oak", "glass", "daub", null, "daub", finishes, joins, glazed: true, innerCorners: innerCorners),
                SidingFloorEntity.SelectiveElements("oak", "glass", "daub", null, finishes, joins, glazed: true, innerCorners: innerCorners),
                SidingWallEntity.DeckElements("west", "oak", "glass", null, null, finishes, joins, default, glazed: true, innerCorners: innerCorners),
                SidingWallEntity.SelectiveElements("cornerout", "oak", "glass", null, null, null, finishes, joins, glazed: true, innerCorners: innerCorners),
                SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, finishes, joins, glazed: false, innerCorners: innerCorners),
            });
    }

    // What a deck draws in the slot of the room-side finish: its whole ledge while that side is bare, nothing
    // behind a finish that fills the slot, and behind a glazed cell's rim the ledge across the opening, out
    // to the cell's edge where a stile has merged away. A diagonal's deck draws none.
    [Fact]
    public void ADecksLedgesFillWhatTheRoomSideFinishLeavesOpen()
    {
        var unjoined = (false, false, false, false);

        Assert.Equal(
            new string[][]
            {
                ["ledge"], ["ledge"], [],
                ["ledgetop"], ["ledgetop", "ledgetopleft"], ["ledgetop", "ledgetopright"], ["ledgetop", "ledgetopleft", "ledgetopright"],
                ["ledgetop"], [], [],
            },
            new[]
            {
                SidingWallEntity.Ledges("wall", roomSideBare: true, glazed: false, unjoined),
                SidingWallEntity.Ledges("wall", roomSideBare: true, glazed: true, (true, false, true, true)),
                SidingWallEntity.Ledges("wall", roomSideBare: false, glazed: false, unjoined),
                SidingWallEntity.Ledges("wall", roomSideBare: false, glazed: true, unjoined),
                SidingWallEntity.Ledges("wall", roomSideBare: false, glazed: true, (false, false, true, false)),
                SidingWallEntity.Ledges("wall", roomSideBare: false, glazed: true, (true, false, false, true)),
                SidingWallEntity.Ledges("wall", roomSideBare: false, glazed: true, (true, true, true, true)),
                SidingWallEntity.Ledges("cornerout", roomSideBare: false, glazed: true, (true, false, false, false)),
                SidingWallEntity.Ledges("diagonal", roomSideBare: true, glazed: false, unjoined),
                SidingWallEntity.Ledges("diagonal", roomSideBare: true, glazed: true, unjoined),
            });
    }

    // A glazed diagonal draws the glass in glazing rails between its corner fillers, and its finish as rims on the
    // rails. Where the run carries on, each piece reaches the corner and the rims also cover the half post's front
    // full height, however the cell is stacked; a deck takes the room side's top rim, as it does on a wall.
    [Fact]
    public void AGlazedDiagonalDrawsRailsAndRimsAndRunsOnToTheCorner()
    {
        var finishes = Dict("""{ "daub": {} }""");
        string[] Elements((bool, bool, bool, bool) joins, bool decked = false) => SidingWallEntity.SelectiveElements(
            "diagonal", "oak", "glass", "daub", null, "daub", finishes, joins, glazed: true, decked: decked);

        string[][] expected =
        [
            [
                "front-rim-top", "front-rim-bottom", "glazing-top", "glazing-bottom", "infill-pane", "back-rim-top", "back-rim-bottom",
                "framing-left", "framing-right",
            ],
            [
                "front-rim-top", "front-rim-bottom", "glazing-top", "glazing-bottom", "infill-pane", "back-rim-top", "back-rim-bottom",
                "front-rim-top-left", "front-rim-bottom-left", "glazing-top-left", "glazing-bottom-left", "infill-pane-left", "back-rim-top-left", "back-rim-bottom-left",
                "front-rim-join-left", "back-rim-join-left", "framing-join-left", "framing-right",
            ],
            [
                "front-rim-bottom", "glazing-bottom", "infill-pane", "back-rim-bottom", "framing-left",
                "front-rim-bottom-right", "glazing-bottom-right", "infill-pane-right", "back-rim-bottom-right",
                "front-rim-join-right", "back-rim-join-right", "framing-join-right",
            ],
            [
                "front-rim-top", "front-rim-bottom", "glazing-top", "glazing-bottom", "infill-pane", "back-rim-bottom",
                "framing-left", "framing-right",
            ],
        ];

        Assert.Equal(
            expected,
            [Elements((false, false, false, false)), Elements((false, false, true, false)), Elements((true, false, false, true)), Elements((false, false, false, false), decked: true)]);
    }

    // Where the glass of one glazed diagonal carries on into the next, the end draws no post: the rails and the pane
    // reach through it and the rims follow the rails. Against a kept post the end draws the half post as before. Where
    // the next cell's stack stops short of this one's, the rail this cell has merged away still crosses the post's place,
    // framing the inside corner the sheet turns there.
    [Fact]
    public void AGlazedDiagonalWhoseGlassRunsOnDrawsRailsAndPaneInPlaceOfThePost()
    {
        var finishes = Dict("""{ "daub": {} }""");
        string[] Elements((bool, bool, bool, bool) joins, (bool, bool) glassRunsOn, string[]? innerCorners = null) => SidingWallEntity.SelectiveElements(
            "diagonal", "oak", "glass", "daub", null, "daub", finishes, joins, glazed: true, innerCorners: innerCorners, glassRunsOn: glassRunsOn);

        string[][] expected =
        [
            [
                "front-rim-top", "front-rim-bottom", "glazing-top", "glazing-bottom", "infill-pane", "back-rim-top", "back-rim-bottom",
                "front-rim-top-left", "front-rim-bottom-left", "glazing-top-left", "glazing-bottom-left", "infill-pane-left", "back-rim-top-left", "back-rim-bottom-left",
                "front-rim-top-join-left", "front-rim-bottom-join-left", "back-rim-top-join-left", "back-rim-bottom-join-left",
                "glazing-top-join-left", "glazing-bottom-join-left", "infill-pane-join-left",
                "framing-right",
            ],
            [
                "front-rim-bottom", "glazing-bottom", "infill-pane", "back-rim-bottom", "framing-left",
                "front-rim-bottom-right", "glazing-bottom-right", "infill-pane-right", "back-rim-bottom-right",
                "front-rim-bottom-join-right", "back-rim-bottom-join-right",
                "glazing-bottom-join-right", "infill-pane-join-right",
            ],
            [
                "front-rim-top", "front-rim-bottom", "glazing-top", "glazing-bottom", "infill-pane", "back-rim-top", "back-rim-bottom",
                "front-rim-top-left", "front-rim-bottom-left", "glazing-top-left", "glazing-bottom-left", "infill-pane-left", "back-rim-top-left", "back-rim-bottom-left",
                "front-rim-top-join-left", "front-rim-bottom-join-left", "back-rim-top-join-left", "back-rim-bottom-join-left",
                "glazing-top-join-left", "glazing-bottom-join-left", "infill-pane-join-left",
                "front-rim-top-right", "front-rim-bottom-right", "glazing-top-right", "glazing-bottom-right", "infill-pane-right", "back-rim-top-right", "back-rim-bottom-right",
                "front-rim-top-join-right", "front-rim-bottom-join-right", "back-rim-top-join-right", "back-rim-bottom-join-right",
                "glazing-top-join-right", "glazing-bottom-join-right", "infill-pane-join-right",
            ],
            [
                "front-rim-top", "front-rim-bottom", "glazing-top", "glazing-bottom", "infill-pane", "back-rim-top", "back-rim-bottom",
                "front-rim-top-left", "front-rim-bottom-left", "glazing-top-left", "glazing-bottom-left", "infill-pane-left", "back-rim-top-left", "back-rim-bottom-left",
                "front-rim-join-left", "back-rim-join-left", "framing-join-left", "framing-right",
            ],
            [
                "front-rim-bottom", "glazing-bottom", "infill-pane", "back-rim-bottom",
                "front-rim-bottom-left", "glazing-bottom-left", "infill-pane-left", "back-rim-bottom-left",
                "front-rim-top-join-left", "front-rim-bottom-join-left", "back-rim-top-join-left", "back-rim-bottom-join-left",
                "glazing-top-join-left", "glazing-bottom-join-left", "infill-pane-join-left",
                "framing-right",
            ],
        ];

        Assert.Equal(
            expected,
            [
                Elements((false, false, true, false), (true, false)),
                Elements((true, false, false, true), (false, true)),
                Elements((false, false, true, true), (true, true)),
                Elements((false, false, true, false), (false, false)),
                Elements((true, false, true, false), (true, false), ["-top-left"]),
            ]);
    }

    // A diagonal takes a finish's own groups as a wall does, draws the plain frame for a rough one, and at
    // each end either the corner filler or, where the next cell carries the run on, every group's piece
    // to the corner and half a post.
    [Fact]
    public void DiagonalEndsInAFillerOrRunsOnToTheCorner()
    {
        var finishes = Dict("""{ "brick": { "Elements": { "front": "front-brick", "back": "back-brick" }, "Styles": ["boards"] } }""");
        string[] Elements((bool, bool, bool, bool) joins) => SidingWallEntity.SelectiveElements(
            "diagonal", "sticks", "wattle", "brick", null, "brick", finishes, joins, glazed: false, styles: ("boards", null, null), frame: "poles");

        string[][] expected =
        [
            ["front-boards", "framing-top", "framing-bottom", "infill", "back-brick", "framing-left", "framing-right"],
            [
                "front-boards", "framing-top", "framing-bottom", "infill", "back-brick",
                "front-boards-left", "framing-top-left", "framing-bottom-left", "infill-left", "back-brick-left", "framing-join-left",
                "framing-right",
            ],
        ];

        Assert.Equal(expected, [Elements((false, false, false, false)), Elements((false, false, true, false))]);
    }

    [Fact]
    public void UnsetKeySurvivesByteRoundTripAsNullNotEmptyString()
    {
        var tree = new TreeAttribute();
        tree.SetString("framing", null);

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Null(SidingWallEntity.NullIfEmpty(reloaded.GetString("framing", null)));
    }

    [Fact]
    public void SetKeySurvivesByteRoundTrip()
    {
        var tree = new TreeAttribute();
        tree.SetString("framing", "oak");

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Equal("oak", SidingWallEntity.NullIfEmpty(reloaded.GetString("framing", null)));
    }

    [Fact]
    public void DeckKeySurvivesByteRoundTrip()
    {
        var tree = new TreeAttribute();
        tree.SetString("deck", "oak");

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Equal("oak", SidingWallEntity.NullIfEmpty(reloaded.GetString("deck", null)));
    }

    [Fact]
    public void DeckSavedBeforeLayersLoadsAsLegacy()
    {
        var tree = new TreeAttribute();
        tree.SetString("deck", "oak");

        Assert.True(SidingWallEntity.ReadLegacyDeck(tree, "oak"));
    }

    [Fact]
    public void LegacyDeckFlagSurvivesByteRoundTrip()
    {
        var tree = new TreeAttribute();
        tree.SetString("deck", "oak");
        tree.SetBool("legacydeck", SidingWallEntity.ReadLegacyDeck(tree, "oak"));

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.True(SidingWallEntity.ReadLegacyDeck(reloaded, "oak"));
    }

    [Fact]
    public void NewDeckStaysNonLegacyAcrossByteRoundTrip()
    {
        var tree = new TreeAttribute();
        tree.SetString("deck", "oak");
        tree.SetBool("legacydeck", false);

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.False(SidingWallEntity.ReadLegacyDeck(reloaded, "oak"));
    }

    [Fact]
    public void DeckLayerKeysSurviveByteRoundTrip()
    {
        string[] keys = ["deckinfill", "deckfront", "deckback", "deckfrontstyle", "deckbackstyle"];
        var tree = new TreeAttribute();
        foreach (string key in keys) tree.SetString(key, key + "-value");
        tree.SetString("deckback", null);

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Equal(
            new string?[] { "deckinfill-value", "deckfront-value", null, "deckfrontstyle-value", "deckbackstyle-value" },
            keys.Select(key => SidingWallEntity.NullIfEmpty(reloaded.GetString(key, null))));
    }

    [Fact]
    public void StepKeysSurviveByteRoundTrip()
    {
        var tree = new TreeAttribute();
        tree.SetString("step", "game:plankstairs-oak-up-north-free");
        tree.SetString("steporientation", "up-north");

        var reloaded = new TreeAttribute();
        reloaded.FromBytes(tree.ToBytes());

        Assert.Equal("game:plankstairs-oak-up-north-free", SidingWallEntity.NullIfEmpty(reloaded.GetString("step", null)));
        Assert.Equal("up-north", SidingWallEntity.NullIfEmpty(reloaded.GetString("steporientation", null)));
    }

    [Fact]
    public void SelectiveElementsSkipsUnbuiltParts()
    {
        Assert.Equal(new string[0], SidingWallEntity.SelectiveElements("wall", null, null, null, null, null, NoElementFinishes, (false, false, false, false), glazed: false));
        Assert.Equal(new[] { "front", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", "daub", null, "daub", NoElementFinishes, (false, false, false, false), glazed: false));
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-top", "framing-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", null, null, null, null, NoElementFinishes, (false, false, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsLeavesTheDeckToItsOwnGroups()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-top", "framing-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", null, null, null, null, NoElementFinishes, (false, false, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsAppendsStepElementsWhenSet()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-top", "framing-bottom", "step-lower", "step-upper-north" },
            SidingWallEntity.SelectiveElements("wall", "oak", null, null, null, null, NoElementFinishes, (false, false, false, false), glazed: false,
                step: new[] { "step-lower", "step-upper-north" }));
    }

    [Theory]
    [InlineData("west", "up-north", new[] { "step-lower", "step-upper-north" })]
    [InlineData("west", "down-north", new[] { "step-upper", "step-lower-north" })]
    [InlineData("west", "up-south", new[] { "step-lower", "step-upper-south" })]
    [InlineData("west", "down-south", new[] { "step-upper", "step-lower-south" })]
    [InlineData("west", "up-east", new string[0])]
    [InlineData("west", "up-west", new string[0])]
    [InlineData("south", "up-west", new[] { "step-lower", "step-upper-north" })]
    [InlineData("south", "down-west", new[] { "step-upper", "step-lower-north" })]
    [InlineData("south", "up-east", new[] { "step-lower", "step-upper-south" })]
    [InlineData("south", "down-east", new[] { "step-upper", "step-lower-south" })]
    [InlineData("south", "up-north", new string[0])]
    [InlineData("south", "up-south", new string[0])]
    [InlineData("east", "up-south", new[] { "step-lower", "step-upper-north" })]
    [InlineData("east", "down-south", new[] { "step-upper", "step-lower-north" })]
    [InlineData("east", "up-north", new[] { "step-lower", "step-upper-south" })]
    [InlineData("east", "down-north", new[] { "step-upper", "step-lower-south" })]
    [InlineData("east", "up-east", new string[0])]
    [InlineData("east", "up-west", new string[0])]
    [InlineData("north", "up-east", new[] { "step-lower", "step-upper-north" })]
    [InlineData("north", "down-east", new[] { "step-upper", "step-lower-north" })]
    [InlineData("north", "up-west", new[] { "step-lower", "step-upper-south" })]
    [InlineData("north", "down-west", new[] { "step-upper", "step-lower-south" })]
    [InlineData("north", "up-north", new string[0])]
    [InlineData("north", "up-south", new string[0])]
    public void StepElementsMapsVanillaOrientationIntoTheUnrotatedFrame(string side, string orientation, string[] expected)
    {
        Assert.Equal(expected, SidingWallEntity.StepElements(side, orientation));
    }

    [Fact]
    public void StepElementsIsEmptyWithoutAnOrientation()
    {
        Assert.Equal(new string[0], SidingWallEntity.StepElements("west", null));
    }

    // Pins StepElements' table to the rotation the collision boxes use (SidingWallBlock.BuildDeckBoxes).
    [Theory]
    [InlineData("west", "north")]
    [InlineData("south", "west")]
    [InlineData("east", "south")]
    [InlineData("north", "east")]
    public void UnrotatedNorthHalfLandsOnTheFacingTheTableClaims(string side, string expectedFacing)
    {
        var origin = new Vec3d(0.5, 0.5, 0.5);
        var box = new Cuboidf(4f / 16, 8f / 16, 0, 1, 1, 8f / 16);
        Cuboidf rotated = box.RotatedCopy(0, SidingWallEntity.RotationYDeg(side), 0, origin);
        double dx = (rotated.X1 + rotated.X2) / 2 - 0.5;
        double dz = (rotated.Z1 + rotated.Z2) / 2 - 0.5;

        string facing = Math.Abs(dx) > Math.Abs(dz)
            ? (dx < 0 ? "west" : "east")
            : (dz < 0 ? "north" : "south");
        Assert.Equal(expectedFacing, facing);
    }

    [Fact]
    public void SelectiveElementsUsesFinishNamedElementsPerFace()
    {
        Assert.Equal(new[] { "front-weatherboard", "secondfront-weatherboard", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-boards" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", "planks", "planks", "planks", PlankFinishes, (false, false, false, false), glazed: false));
    }

    // A style overrides the entry's Elements default per face, and only for the faces it is set on.
    [Fact]
    public void SelectiveElementsPrefersThePerFaceStyleOverTheEntryDefault()
    {
        Assert.Equal(new[] { "front-boards", "secondfront-weatherboard", "framing-left", "framing-right", "framing-top", "framing-bottom", "infill", "back-weatherboard" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", "planks", "planks", "planks", PlankFinishes, (false, false, false, false), glazed: false,
                ("boards", null, "weatherboard")));
    }

    [Fact]
    public void SelectiveElementsForBottomOfAStackSkipsOnlyTheTopPlate()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-bottom", "infill", "infill-top" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, (true, false, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsForTopOfAStackSkipsOnlyTheBottomPlate()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "framing-top", "infill", "infill-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, (false, true, false, false), glazed: false));
    }

    [Fact]
    public void SelectiveElementsForAFilledMiddleCellSkipsBothPlatesAndExtendsInfillBothWays()
    {
        Assert.Equal(new[] { "framing-left", "framing-right", "infill", "infill-top", "infill-bottom" },
            SidingWallEntity.SelectiveElements("wall", "oak", "wattle", null, null, null, NoElementFinishes, (true, true, false, false), glazed: false));
    }

    [Theory]
    [InlineData(1, new[] { true })]
    [InlineData(2, new[] { false, true })]
    [InlineData(3, new[] { false, true, true })]
    [InlineData(4, new[] { false, true, false, true })]
    public void EverySecondCellUpAStackKeepsItsTopPlateAsACrossBeam(int height, bool[] expectedTopPlates)
    {
        bool[] topPlates = Enumerable.Range(0, height)
            .Select(cellsBelow => !SidingWallBlock.JoinsAbove(cellsBelow < height - 1, cellsBelow))
            .ToArray();

        Assert.Equal(expectedTopPlates, topPlates);
    }

    [Theory]
    [InlineData("west", 0)]
    [InlineData("south", 90)]
    [InlineData("east", 180)]
    [InlineData("north", 270)]
    public void RotationYDegMatchesCollisionBoxRotation(string side, float expectedDegrees)
    {
        Assert.Equal(expectedDegrees, SidingWallEntity.RotationYDeg(side));
    }

    // Anything the mesh is built from has to reach the key, or two differently-shaped walls share
    // one cached mesh - a merged glazed cell and an unmerged one being the newest way to get that wrong.
    [Fact]
    public void CacheKeyDistinguishesEveryMeshInput()
    {
        string[] keys =
        [
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("cornerout", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "south", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "veryaged", "wattle", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "glass-plain", "daub", "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", null, "planks", "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", null, "brick", (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", null, (false, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (true, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, true, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, true, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, true)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), ("boards", null, null)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), (null, "boards", null)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), (null, null, "weatherboard")),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak"),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, null, "game:plankstairs-oak-up-north-free"),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, null, null, "up-north"),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, "wattle"),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, "glass-plain"),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, "planks"),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, null, "planks"),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, null, null, ("boards", null)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, null, null, (null, "boards")),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, null, null, default, (true, false, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), alternate: 1),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, null, null, default, (false, true, false, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, null, null, default, (false, false, true, false)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), default, "oak", null, null, null, null, null, default, (false, false, false, true)),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), innerCorners: ["-top-left"]),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), innerCorners: ["-top-right"]),
            SidingWallEntity.CacheKey("wall", "west", "oak", "wattle", "daub", "planks", "brick", (false, false, false, false), deckInnerCorners: ["-top-left"]),
        ];

        Assert.Equal(keys, keys.Distinct());
    }

    [Fact]
    public void GlassGroupsOfTheWallAndItsDeckAreInfillElements()
    {
        Assert.Equal(
            [true, true, true, false, false, false, true, false, true, false],
            new[]
            {
                "infill-pane", "infill-top", "deck-west-infill-pane", "deck-west-glazing-top", "framing-left", "deck-north-front-hboards",
                "ledge-west-infill-pane", "ledge-west-glazing-top", "ledgetopleft-south-infill-pane", "ledgetop-south-glazing-top",
            }
                .Select(SidingWallEntity.IsInfillElement));
    }
}
