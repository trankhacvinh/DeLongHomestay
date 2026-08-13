## UI Pro Max Search Results
**Domain:** ux | **Query:** dashboard calendar modal form table animation accessibility keyboard focus mobile responsive
**Source:** ux-guidelines.csv | **Found:** 10 results

### Result 1
- **Category:** Responsive
- **Issue:** Table Handling
- **Platform:** Web
- **Description:** Tables can overflow on mobile
- **Do:** Use horizontal scroll or card layout
- **Don't:** Wide tables breaking layout
- **Code Example Good:** overflow-x-auto wrapper
- **Code Example Bad:** Table overflows viewport
- **Severity:** Medium

### Result 2
- **Category:** Interaction
- **Issue:** Focus States
- **Platform:** All
- **Description:** Keyboard users need visible focus indicators
- **Do:** Use visible focus rings on interactive elements
- **Don't:** Remove focus outline without replacement
- **Code Example Good:** focus:ring-2 focus:ring-blue-500
- **Code Example Bad:** outline-none without alternative
- **Severity:** High

### Result 3
- **Category:** Accessibility
- **Issue:** Keyboard Navigation
- **Platform:** Web
- **Description:** All functionality accessible via keyboard
- **Do:** Tab order matches visual order
- **Don't:** Keyboard traps or illogical tab order
- **Code Example Good:** tabIndex for custom order
- **Code Example Bad:** Unreachable elements
- **Severity:** High

### Result 4
- **Category:** Accessibility
- **Issue:** Form Labels
- **Platform:** All
- **Description:** Inputs must have associated labels
- **Do:** Use label with for attribute or wrap input
- **Don't:** Placeholder-only inputs
- **Code Example Good:** <label for='email'>
- **Code Example Bad:** placeholder='Email' only
- **Severity:** High

### Result 5
- **Category:** Forms
- **Issue:** Mobile Keyboards
- **Platform:** Mobile
- **Description:** Show appropriate keyboard for input type
- **Do:** Use inputmode attribute
- **Don't:** Default keyboard for all inputs
- **Code Example Good:** inputmode='numeric'
- **Code Example Bad:** Text keyboard for numbers
- **Severity:** Medium

### Result 6
- **Category:** Accessibility
- **Issue:** Skip Links
- **Platform:** Web
- **Description:** Allow keyboard users to skip navigation
- **Do:** Provide skip to main content link
- **Don't:** No skip link on nav-heavy pages
- **Code Example Good:** Skip to main content link
- **Code Example Bad:** 100 tabs to reach content
- **Severity:** Medium

### Result 7
- **Category:** Responsive
- **Issue:** Mobile First
- **Platform:** Web
- **Description:** Design for mobile then enhance for larger
- **Do:** Start with mobile styles then add breakpoints
- **Don't:** Desktop-first causing mobile issues
- **Code Example Good:** Default mobile + md: lg: xl:
- **Code Example Bad:** Desktop default + max-width queries
- **Severity:** Medium

### Result 8
- **Category:** Animation
- **Issue:** Duration Timing
- **Platform:** All
- **Description:** Animations should feel responsive not sluggish
- **Do:** Use 150-300ms for micro-interactions
- **Don't:** Use animations longer than 500ms for UI
- **Code Example Good:** transition-all duration-200
- **Code Example Bad:** duration-1000
- **Severity:** Medium

### Result 9
- **Category:** Responsive
- **Issue:** Viewport Meta
- **Platform:** Web
- **Description:** Set viewport for mobile devices
- **Do:** Use width=device-width initial-scale=1
- **Don't:** Missing or incorrect viewport
- **Code Example Good:** <meta name='viewport'...>
- **Code Example Bad:** No viewport meta tag
- **Severity:** High

### Result 10
- **Category:** Responsive
- **Issue:** Touch Friendly
- **Platform:** Web
- **Description:** Mobile layouts need touch-sized targets
- **Do:** Increase touch targets on mobile
- **Don't:** Same tiny buttons on mobile
- **Code Example Good:** Larger buttons on mobile
- **Code Example Bad:** Desktop-sized targets on mobile
- **Severity:** High

