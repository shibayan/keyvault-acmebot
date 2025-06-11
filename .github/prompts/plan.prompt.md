---
mode: 'edit'
description: 'Plan an implementation'
---

Your goal is to generate an implementation plan for a specification document provided to you.

RULES:
- Keep implementations simple, do not over architect
- Do not generate real code for your plan, pseudocode is OK
- For each step in your plan, include the objective of the step, the steps to achieve that objective, and any necessary pseudocode.
- Call out any necessary user intervention required for each step
- Consider accessibility part of each step and not a separate step

FIRST:

- Understand the current project structure and dependencies.
- Familiarize yourself with the existing codebase and architecture.

- Review the attached specification document to understand the requirements and objectives.

THEN:
- Create a detailed implementation plan that outlines the steps needed to achieve the objectives of the specification document.
- The plan should be structured, clear, and easy to follow.
- Structure your plan as follows, and output as Markdown code block

```markdown
# Implementation Plan for [Spec Name]

- [ ] Step 1: [Brief title]
  - **Task**: [Detailed explanation of what needs to be implemented]
  - **Files**: [Maximum of 20 files, ideally less]
    - `path/to/file1.ts`: [Description of changes], [Pseudocode for implementation]
  - **Dependencies**: [Dependencies for step]

[Additional steps...]
```

- After the steps to implement the feature, add a step to build and run the app
- Add a step to write unit and UI tests for the feature
- Add a step to run all unit and UI tests as last step (with no -destination set to ensure active simulator is used)

NEXT:

- Iterate with me until I am satisifed with the plan

FINALLY: 

- Output your plan in #folder:../../docs/plans/plan-name.md
- DO NOT start implementation without my permission.