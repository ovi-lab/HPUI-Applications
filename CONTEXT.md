# HPUI Applications

Showcase applications built on HPUI (Hand Proximate User Interface): interacting with UI by pointing with the hand/fingers instead of touching or holding controllers. The interaction machinery itself (interactables, interactor, gesture data types) lives in the upstream HPUI packages; this repo holds the applications built on top of them.

## Language

### Interaction

**HPUI**:
Hand Proximate User Interface — the interaction technique where a user points at UI elements with their hand (fingers) to select, press, and manipulate them.

**Interactable**:
A scene object that can receive HPUI pointing input. _Avoid_: target, button (reserved for concrete widgets like Numpad buttons).

**Gesture**:
A complete hand interaction against an interactable, from contact through release. Concrete kinds: tap, double tap, long press, flick.

**Discrete gesture**:
A gesture with a discrete outcome fired once when the gesture resolves (tap, double tap, long press, flick). Contrast with continuous interaction. _Avoid_: discreet.

**Continuous interaction**:
Position-stream interaction along an interactable's surface while the finger stays tracked (sliders, surface dragging). Yields a stream of position events rather than a single discrete outcome.

**Gesture event**:
A per-frame event from an interactable while a gesture is in progress; carries pointer position, cumulative movement, and elapsed time. Discrete gesture detection is built from these.

**Alpha (contact threshold)**:
The minimum duration a contact must be held before it registers as a gesture start; contacts shorter than alpha are discarded as transients. Referred to by the Greek letter in specs and code; the default value is tunable. _Avoid_: debounce, press-and-hold delay.

**Beta (exit threshold)**:
The duration a surface may be without contact before a gesture is considered ended; momentary contact losses shorter than beta are stitched into one ongoing gesture. Referred to by the Greek letter in specs and code; the default value is tunable. _Avoid_: gesture timeout.

**Premature trigger**:
A short-press style firing of a gesture result before the gesture completes (e.g. button-like input), used when a gesture should commit early. Applied when only one gesture outcome is possible on a surface: if the contact persists without completing (the user holds on waiting for a response), the outcome fires early and gesture detection is disabled until the contact lifts. _Avoid_: discreet.

### Keyboard / text input

**Finger row**:
An interactable spanning one finger's side; the raw input surface for keyboard swipe input. Rows are remapped into keyboard space. Builds on the ThumbSwype mapping: the top row is the index finger, the middle row the middle finger, the bottom row the ring finger — so both naming schemes (top/mid/bottom and index/middle/ring) are canonical.

**Keyboard space**:
The normalized (0,1) coordinate space of the keyboard surface; all keyboard input and trajectory data lives in this space, regardless of which finger row produced it. _Avoid_: screen space, interactable space (per-row raw coordinates).

**Layout**:
The JSON description of the keyboard: key positions and letters, laid out in a unit square.

**Key**:
One visual key quad generated from the layout. _Avoid_: letter quad (implementation detail).

**Cursor**:
The visual marker showing the current position in keyboard space during a gesture. Distinct from the text caret.

**Trajectory**:
The sequence of raw (unfiltered) keyboard-space positions recorded during one gesture. Filtered positions are streamed live to consumers but are not part of the trajectory.

**Swipe**:
A gesture across the keyboard, sent as a trajectory to the recognition server to be decoded into a word.

**Word recognition**:
Server-side decoding of a swipe trajectory into ranked candidate words, using the current text as language-model context.

**Premature trigger**:
A short-press style firing of a gesture result before the gesture completes (e.g. button-like input), used when a gesture should commit early.

### Text editing

**Word**:
The unit of text content and manipulation; the document is a sequence of words, and the caret and selection operate on whole words only.

**Atomic action**:
The smallest unit of editor change that is recorded and reversible as a whole; one atomic action is one state transition of the editor.

**Configuration**:
A complete declarative description of the system's module selections and tunable parameters, sufficient to instantiate the entire system from scratch and to re-instantiate it at runtime. Swapping configurations is the mechanism for switching between system variants.

**Caret**:
The text insertion position in the input field. Distinct from the keyboard cursor (which drives it).

**Visual selection**:
A highlighted text selection controlled programmatically from an anchor (selection origin) and a focus position that follows caret movement.

### Color picker

**Mode**:
Which slider set drives the target color: RGB, HSV, or Touch. Exactly one mode is active; the others sync to reflect the current color.

**Target color**:
The single color value the picker edits and applies to the target object.

**Target object**:
The scene object whose material receives the target color.
