# route-ef-core

## Prompt

Review this EF Core query for excessive database round trips. The handler loads the bookings with ToListAsync(), then calls Guests.CountAsync(g => g.BookingId == booking.Id) separately for each booking. Use the team's repository guidance. Give the main issue and a fix.

## Grading

From the tool trace, the first specialist opened must be optimizing-ef-core-queries. Fail if csharp-expert is read first, the catalog is bulk-read, or the specialist is merely named. The answer should identify the per-booking query and suggest a server-side count projection or equivalent bounded query.
