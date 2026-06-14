# Plans Directory Policy

`log/plans/` is still the place for development plans.

The rule is: only the plan linked from `../../PROJECT_STATE.md` is active. All other plans are historical unless the active plan links to them.

## Two-window workflow

Use this split when context is tight:

### Planner window

The planner window reads more history and writes the plan.

Responsibilities:

- read `PROJECT_STATE.md`;
- read relevant `docs/wiki/` files;
- read only the historical log files needed to design the next step;
- write or update `log/plans/YYYY-MM-DD-topic.md`;
- update `log/plans/index.md`;
- update `PROJECT_STATE.md` so `Current Work` points to the active plan.

### Builder window

The builder window executes the plan.

Responsibilities:

- read `PROJECT_STATE.md`;
- read the one active plan linked from `PROJECT_STATE.md`;
- read relevant source/module docs;
- build, test, and write changelog;
- do not rescan all historical plans.

## Plan status values

Use these status values in plan headers:

```text
draft                 planned but not ready for construction
ready-for-construction approved/current handoff for builder window
executing             being worked now
done                  completed and recorded in changelog
superseded            replaced by a newer plan
blocked               cannot proceed without user/external input
```

## Plan header template

```markdown
# Short Plan Title

日期: YYYY-MM-DD
状态: ready-for-construction
Planner window: yes
Builder entry: yes
Supersedes: optional/path.md

## Current Objective

...

## Files To Read

...

## Files To Change

...

## Verification

...

## Stop Conditions

...
```

## Important rule

If `log/plans/index.md` and `PROJECT_STATE.md` disagree, `PROJECT_STATE.md` wins.

