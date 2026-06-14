# Planning Workflow

Last updated: 2026-06-13

This project supports a two-window agent workflow:

- one window does planning and context gathering;
- another window does construction with a small context footprint.

## Where Plans Live

Development plans live in:

```text
log/plans/
```

The active plan is not discovered by scanning that directory. The active plan is the one linked from:

```text
PROJECT_STATE.md
```

This prevents old plans from being misread as current work.

## Planner Window

Use the planner window for context-heavy work:

1. Read `PROJECT_STATE.md`.
2. Read `docs/wiki/architecture.md` and relevant module docs.
3. Read historical `log/` files only as needed.
4. Write or update a plan under `log/plans/YYYY-MM-DD-topic.md`.
5. Update `log/plans/index.md`.
6. Update `PROJECT_STATE.md` so it links to the plan the builder should execute.

The planner may read old changelogs and guidance, but it must summarize the decision into the active plan. The builder should not have to rediscover it.

## Builder Window

Use the builder window for implementation:

1. Read `PROJECT_STATE.md`.
2. Read the active plan linked there.
3. Read only source/module docs needed for the files being changed.
4. Implement.
5. Verify.
6. Write changelog.

The builder should not bulk-read `log/` and should not choose between old plans.

## Active Plan Rules

There should be one active construction plan at a time.

Acceptable active statuses:

```text
ready-for-construction
executing
```

Historical statuses:

```text
done
superseded
blocked
```

Draft status:

```text
draft
```

Draft plans can exist, but `PROJECT_STATE.md` should not point a builder at a draft unless the user explicitly asks the builder to review or refine the draft.

## Handoff Quality Bar

A builder-ready plan must include:

- current objective;
- files to read;
- files to change;
- verification commands;
- stop conditions;
- non-goals.

If a plan is missing those, the builder should fix the plan or ask for clarification before large edits.

