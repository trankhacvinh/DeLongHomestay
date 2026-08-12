## UI Pro Max Search Results
**Domain:** web | **Query:** form input aria focus keyboard semantic responsive table
**Source:** web-interface.csv | **Found:** 10 results

### Result 1
- **Category:** Accessibility
- **Issue:** Form Control Labels
- **Platform:** Web
- **Description:** All form controls need labels or aria-label
- **Do:** Use label element or aria-label
- **Don't:** Input without accessible name
- **Code Example Good:** <label for='email'>Email</label><input id='email' />
- **Code Example Bad:** <input placeholder='Email' />
- **Severity:** Critical

### Result 2
- **Category:** Forms
- **Issue:** Semantic Input Types
- **Platform:** Web
- **Description:** Use semantic input type attributes
- **Do:** Use email/tel/url/number types
- **Don't:** text type for everything
- **Code Example Good:** <input type='email' />
- **Code Example Bad:** <input type='text' /> // for email
- **Severity:** Medium

### Result 3
- **Category:** Accessibility
- **Issue:** Semantic HTML
- **Platform:** Web
- **Description:** Use semantic HTML before ARIA attributes
- **Do:** Use button/a/label elements
- **Don't:** Div with role attribute
- **Code Example Good:** <button onClick={fn}>Submit</button>
- **Code Example Bad:** <div role='button' onClick={fn}>Submit</div>
- **Severity:** High

### Result 4
- **Category:** Accessibility
- **Issue:** Keyboard Handlers
- **Platform:** Web
- **Description:** Interactive elements must support keyboard interaction
- **Do:** Add onKeyDown alongside onClick
- **Don't:** Click-only interaction
- **Code Example Good:** <div onClick={fn} onKeyDown={fn} tabIndex={0}>
- **Code Example Bad:** <div onClick={fn}>
- **Severity:** High

### Result 5
- **Category:** Forms
- **Issue:** Autocomplete Attribute
- **Platform:** Web
- **Description:** Inputs need autocomplete attribute for autofill
- **Do:** Add appropriate autocomplete value
- **Don't:** Missing autocomplete
- **Code Example Good:** <input autocomplete='email' type='email' />
- **Code Example Bad:** <input type='email' />
- **Severity:** High

### Result 6
- **Category:** Focus
- **Issue:** Visible Focus States
- **Platform:** Web
- **Description:** All interactive elements need visible focus states
- **Do:** Use :focus-visible with ring/outline
- **Don't:** No focus indication
- **Code Example Good:** focus-visible:ring-2 focus-visible:ring-blue-500
- **Code Example Bad:** outline-none // no replacement
- **Severity:** Critical

### Result 7
- **Category:** Accessibility
- **Issue:** Aria Live
- **Platform:** Web
- **Description:** Async updates need aria-live for screen readers
- **Do:** Add aria-live='polite' for dynamic content
- **Don't:** Silent async updates
- **Code Example Good:** <div aria-live='polite'>{status}</div>
- **Code Example Bad:** <div>{status}</div> // no announcement
- **Severity:** Medium

### Result 8
- **Category:** Focus
- **Issue:** Never Remove Outline
- **Platform:** Web
- **Description:** Never remove outline without providing replacement
- **Do:** Replace outline with visible alternative
- **Don't:** Remove outline completely
- **Code Example Good:** focus:outline-none focus:ring-2
- **Code Example Bad:** focus:outline-none // nothing else
- **Severity:** Critical

### Result 9
- **Category:** Accessibility
- **Issue:** Decorative Icons
- **Platform:** Web
- **Description:** Decorative icons should be hidden from screen readers
- **Do:** Add aria-hidden='true' to decorative icons
- **Don't:** Decorative icon announced
- **Code Example Good:** <Icon aria-hidden='true' />
- **Code Example Bad:** <Icon /> // announced as 'image'
- **Severity:** Medium

### Result 10
- **Category:** Anti-Pattern
- **Issue:** Outline Replacement
- **Platform:** Web
- **Description:** Never use outline-none without replacement
- **Do:** Provide visible focus replacement
- **Don't:** Remove outline with nothing
- **Code Example Good:** focus:outline-none focus:ring-2 focus:ring-blue-500
- **Code Example Bad:** focus:outline-none // alone
- **Severity:** Critical

