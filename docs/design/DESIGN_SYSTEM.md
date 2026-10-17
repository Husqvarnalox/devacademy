# DevAcademy design system

Direction: an engineering notebook. Warm paper and ink, one teal accent, typography carries the identity. Dense enough for an IDE, calm enough for a beginner.

## Principles
1. Type first. Hierarchy comes from size, weight and spacing, not from boxes and colors.
2. One accent (teal) for the primary action, selection and progress. Status colors are reserved for status.
3. Flat. 1px lines, no gradients, glass, glow or decorative shadows. Shadows only on floating layers (flyouts, dialogs).
4. Icons are functional (Lucide only): navigation, toolbars, status markers, icon-only buttons. Never decorative.
5. Plain Russian. Explain jargon on first use. Icon-only buttons always have a tooltip and an automation name.

## Tokens
Defined in `App.axaml` (Light and Dark dictionaries). Brush keys: Bg, Surface, SurfaceSunken, Line, LineStrong, Text, TextSecondary, TextMuted, Accent, AccentHover, AccentSubtle, Success, Warn, Danger (+ Subtle variants). Legacy aliases are kept for C# controls.
Text and its tinted backgrounds meet 4.5:1 (checked with a script; see the handoff for the numbers).

Spacing: 4 / 8 / 12 / 16 / 24 / 32 / 48. Radius: 6 controls, 8 surfaces. Heights: 32 default, 36 primary on hub pages, 28 compact in IDE toolbars. Hit targets are at least 28 px. Hub pages: max width 1040, 32 px gutters. Transitions 120 ms.

## Typography
IBM Plex Sans (UI, three weights: Regular, Medium, SemiBold) and IBM Plex Mono (code), embedded under `Assets/Fonts` (OFL texts included).

| Class | Size/line | Use |
|---|---|---|
| display | 28/34 | page title |
| h1 | 22/28 | section page title |
| h2 | 17/24 | section heading |
| h3 | 14/20 | group heading |
| body | 14/22 | default |
| reading | 15/25, about 680 px | lesson theory |
| small | 13/18 | secondary text |
| caption | 12/16 | meta |
| mono | 13/20 | code |
| overline | 11/16, +0.06em, uppercase | labels |

## Components (Styles/Theme.axaml)
- Buttons: `primary` (one per view), default (secondary), `ghost`, `danger`, `compact` (28), `large` (36), `icon`. Hover, pressed, disabled and a visible focus ring are defined.
- Tabs: text with a 2px accent underline. Inputs: 32 px, 1px line, accent focus border.
- Badges `badge` (+accent, success, warn, danger): subtle tinted chip, never more than a few per row.
- Progress: 4px bar. Overline label. Callouts `callout` (+warn, danger, success).
- Rows: `Button.row` in `ItemsControl.rows` inside a `panel`; hover SurfaceSunken, selected AccentSubtle plus a 2px marker.
- Choices: `Button.tile` (+selected), `ListBox.choices`, `ListBox.segmented`, quiz `Border.option` (selected / correct / wrong).
- Icons: `<c:Icon Kind=...>` with Lucide geometries in `Styles/Icons.axaml`, stroked with the inherited foreground.

## Editor
Editor background is Surface, current line SurfaceSunken, gutter TextMuted. Syntax token colors stay the familiar TextMate Light+/Dark+ palette.

## Do
- Use one primary button per view; use ghost for the rest.
- Keep lists as rows with hairlines; use tiles only for choices.
- Hide empty states of numbers (no zero tiles); show a short plain sentence instead.

## Don't
- No gradients, glass, glow, blobs, emoji as UI, icons beside headings, or fake statistics.
- Do not add a fourth font weight or a second accent color.
- Do not put status color on large surfaces.
