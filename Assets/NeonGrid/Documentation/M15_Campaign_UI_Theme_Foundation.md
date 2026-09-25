# M15-D1 Campaign UI Theme Foundation

## Identity and scope

The campaign-facing visual identity is **Technical Neon Interface**: a calm restoration-terminal language that belongs beside the Technical Neon production gameplay skin without borrowing gameplay tile housings. M15-D1 established the isolated visual prototype and M15-D2 associated that accepted theme with the production `neon_grid_main` campaign. The accepted M13/M14 presentation remains the null-theme fallback.

## Visual language

- Backgrounds use near-black navy (`#050810`) with extremely subtle graphite-blue depth fields (`#0B1220`). They contain no playable wire motifs or bright decoration.
- Panels use a graphite/navy surface (`#101A2B`), a recessed inset (`#08111E`), a restrained cyan edge, a 2 px keyline, a 12 px inset, and a low-alpha shadow. Panels read as interface modules rather than gameplay tiles.
- Primary interface accents use cyan (`#27D8E8`). Neutral information uses cool blue-gray (`#7895AA`); restored/success state uses teal (`#32D6A0`); restrained attention uses amber (`#D2A24C`). Magenta and objective yellow remain gameplay semantics.
- Titles use cool white (`#EAF8FF`), body copy uses readable blue-white (`#D6E8F2`), and secondary information uses subdued blue-gray (`#819AAC`). Existing project fonts, authored capitalization, and accepted M14-C sizes remain authoritative.
- Buttons use a dark graphite body with a restrained cyan edge and explicit normal, highlighted, pressed, and disabled colors. Existing sizes, hit areas, labels, and navigation behavior are unchanged.
- Dividers and keylines exist only where they establish hierarchy. Spacing is preferred over decorative rules, and keylines must not crowd text.

## Representative states

- Intro uses one restrained narrative module around the existing four-page content. Copy, page count, navigation, and persistence semantics remain unchanged.
- Selector/System Briefing retains the accepted 3+3+3+1 grid, typography, stars, ordering, and Back placement. Locked cards use a muted neutral surface and lower text contrast; available cards use the primary cyan affordance; completed cards use a stable success surface. Structure, contrast, and interaction state distinguish them without relying on hue alone.
- Restoration status uses the existing event-driven module with a restrained success-teal edge and surface. Copy, four non-final events, timing, and final-chapter omission are unchanged.
- Ending uses the strongest success accent in this family while remaining terminal-like. Existing copy and Return to City behavior are unchanged.
- City Map support is limited to header, global stars, and shared navigation chrome. Building silhouettes, node positions, paths, restoration animation, and environmental art are not part of D1.

## Readability and mobile performance

The accepted text sizes are minimums and are not reduced for style. Body copy remains readable without glow; semantic state remains legible through contrast and structure; buttons retain strong label contrast. The prototype targets 1080x1920, 1080x2340, and 720x1280 through the existing reference-canvas system.

Depth uses static uGUI layers, shared color data, and lightweight outlines. The theme adds no update loop, procedural texture generation, custom shader, unique material per panel, nested masks, scanlines, bloom, or continuously animated decoration. Static hierarchy is built once per preview mode.

## Prototype isolation

`M15_CampaignUiVisualPrototype` reads the production campaign and narrative assets and constructs the authoritative existing views with in-memory preview progress. It never writes campaign progress and remains excluded from build settings. M15-D2 reuses the same accepted theme asset through the optional `CampaignDefinition.CampaignUiTheme` association; the production runtime does not route through the prototype scene. Campaigns without that association—including the five source vertical slices—continue to use the accepted M13/M14 fallback.
