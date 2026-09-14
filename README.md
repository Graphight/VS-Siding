# Siding - Layered Wall Building for Vintage Story

A Vintage Story mod that replaces solid one-block walls with a thin, layered wall system: pick a framing material, an infill material, and a finish for each face, and build walls that read like actual construction instead of a stack of full blocks.

Inspired by how the [Roofing](https://mods.vintagestory.at/show/mod/30143) mod handles pitched roofs: one shared shape, material swapped in via config, instead of hand-authoring every combination.

**Status**: early scaffolding, no gameplay yet.

Framing plus infill makes a complete wall that seals a room the way solid blocks do, and the infill material decides whether it counts as a cooling (cellar) wall. Face finishes are appearance only. There is no custom heat simulation beyond that - a possible future extension, not a v1 goal.

---

## Building from Source

```bash
./build.sh
```

Requires the `VINTAGE_STORY` environment variable to point at your Vintage Story install (or a `Directory.Build.props.user` copied from `Directory.Build.props.user.example`).

## License

[MIT](./LICENSE)
