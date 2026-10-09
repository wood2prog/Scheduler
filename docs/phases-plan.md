# Job phases (Design / Construction / Delivery) - plan and progress

## Requirements (as agreed)

Jobs start as a **Prospect**: a placeholder entered as soon as the user hears they might work on
it. It has no bar on the timeline, only an optional delivery target date. Once it becomes real, the
bar is split into three consecutive phases:

1. **Design** - starts on the job's start date (set by hand, or pinned to today as today).
2. **Construction** - starts when the job moves out of Design. While in progress its end follows today.
3. **Delivery** - starts when Construction is complete. Has an optional **delivery target date**
   (a marker on one future day, settable at job start or any time later). The bar keeps extending
   to today, past the target if need be (the part past the target is shown differently). When the
   job is marked finished, the end date is set to that day and the bar stops.

- A **Phase** dropdown replaces separate check marks: No phases / Prospect / Design / Construction /
  Delivery / Finished. New jobs start as Prospect. Moving forward stamps today as the date of each
  boundary crossed (leaving Prospect also sets the job's start date to today). Moving back extends the
  earlier phase up to today, overwriting (clearing) the dates of the phase(s) reverted from.
- Every phase date is editable, any way the user wants (no validation beyond basic sanity - the user
  accepts responsibility). Ideally by hovering a seam between phases in the Gantt chart and dragging it.
- Existing jobs keep their current single-color bar. Phases are opt-in per job (Phase is null for them).
- The existing duration report still covers the whole job, start to finish. Add separate reports
  per phase (Design, Construction, Delivery).

## Data model

`Job` gains: `Phase` (nullable; null = no phases), `ConstructionStartDate?` (seam Design|Construction),
`DeliveryStartDate?` (seam Construction|Delivery), `DeliveryTargetDate?`. Existing `StartDate`,
`EndDate`, `Completed` keep their meaning: start of Design, end of Delivery, job finished.

## Steps

- [x] 1. Data model: Job fields, SQLite columns + migration, repository read/write.
- [x] 2. Application logic: change phase (stamp / clear dates), resolve a job's phase segments for a given day.
- [x] 3. Edit UI: Phase dropdown, delivery target date, editing of phase dates; convert existing jobs.
- [x] 4. Gantt drawing: three colored segments, delivery target marker, overdue shading.
- [ ] 5. Gantt seam hover + drag to change a phase date.
- [ ] 6. Per-phase reports (Design / Construction / Delivery durations) in the Reports dropdown.
- [ ] 7. Wrap-up: update CLAUDE.md / README, bump installer version if releasing.

## Open questions / decisions log

- New jobs default to Prospect (decided). Going back a phase clears later dates (decided).
- Where do the phase dates get typed (as opposed to dragged)? (pending - decide in step 3)
