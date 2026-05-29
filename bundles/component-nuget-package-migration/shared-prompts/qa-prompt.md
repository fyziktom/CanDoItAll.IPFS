# QA Prompt

Use this prompt for validation:

```text
Validate the completed subbundle against its acceptance checklist and proof requirements.
For package proof, confirm old component source-project references are gone and restored package IDs match the ExternalPackages artifacts.
For UI proof, inspect screenshots rather than only recording their paths: look for missing BaseLib styling, missing icons, unstyled controls, broken chart/mermaid chrome, overlap, clipping, or major spacing drift.
If proof is weak, mark the subbundle blocked or reopen it instead of passing it.
```
