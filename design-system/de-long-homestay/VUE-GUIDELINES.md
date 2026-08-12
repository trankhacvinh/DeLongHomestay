## UI Pro Max Stack Guidelines
**Stack:** vue | **Query:** razor server rendered progressive enhancement modal forms table responsive accessibility
**Source:** stacks/vue.csv | **Found:** 6 results

### Result 1
- **Category:** SSR
- **Guideline:** Handle hydration mismatches
- **Description:** Client/server content must match
- **Do:** ClientOnly for browser-only content
- **Don't:** Different content server/client
- **Code Good:** <ClientOnly><BrowserWidget/></ClientOnly>
- **Code Bad:** <div>{{ Date.now() }}</div>
- **Severity:** High
- **Docs URL:** 

### Result 2
- **Category:** Forms
- **Guideline:** Use VeeValidate or FormKit
- **Description:** Form validation libraries
- **Do:** VeeValidate for complex forms
- **Don't:** Manual validation logic
- **Code Good:** useField useForm from vee-validate
- **Code Bad:** Custom validation in each input
- **Severity:** Medium
- **Docs URL:** 

### Result 3
- **Category:** Testing
- **Guideline:** Test component behavior
- **Description:** Focus on inputs and outputs
- **Do:** Test props emit and rendered output
- **Don't:** Test internal implementation
- **Code Good:** expect(wrapper.text()).toContain()
- **Code Bad:** expect(wrapper.vm.internalState)
- **Severity:** Medium
- **Docs URL:** 

### Result 4
- **Category:** Forms
- **Guideline:** Use v-model modifiers
- **Description:** Built-in input handling
- **Do:** .lazy .number .trim modifiers
- **Don't:** Manual input parsing
- **Code Good:** <input v-model.number="age">
- **Code Bad:** <input v-model="age"> then parse
- **Severity:** Low
- **Docs URL:** https://vuejs.org/guide/essentials/forms.html#modifiers

### Result 5
- **Category:** Accessibility
- **Guideline:** Use semantic elements
- **Description:** Proper HTML elements in templates
- **Do:** button nav main for purpose
- **Don't:** div for everything
- **Code Good:** <button @click>
- **Code Bad:** <div @click>
- **Severity:** High
- **Docs URL:** 

### Result 6
- **Category:** Accessibility
- **Guideline:** Bind aria attributes dynamically
- **Description:** Keep ARIA in sync with state
- **Do:** :aria-expanded="isOpen"
- **Don't:** Static ARIA values
- **Code Good:** :aria-expanded="menuOpen"
- **Code Bad:** aria-expanded="true"
- **Severity:** Medium
- **Docs URL:** 

