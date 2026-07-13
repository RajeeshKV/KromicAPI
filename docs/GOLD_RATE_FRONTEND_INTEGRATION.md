# Gold Rate Admin Frontend Changes

This document lists the frontend changes needed for the gold-rate admin feature.

The backend is already implemented in this API repo. The frontend app is not present in this workspace, so these changes should be applied in the admin frontend repository.

All endpoints are protected by the existing admin JWT role. Send the same bearer token used by other admin pages.

## Scope

Add the following admin UI features:

- Show today's R22KT gold rate on the admin dashboard.
- Refresh and store the current rate whenever an admin logs in or loads the dashboard.
- Add a manual "Fetch gold rate" button that fetches fresh data and sends email notifications.
- Add a historical gold-rate table.
- Add date/range filters: `1D`, `7D`, `1M`, `3M`, `6M`, `1Y`, and optional custom date range.
- Show the lowest stored rate clearly.
- Show whether normal email and lowest-rate alert email were sent.

## Environment Variables

These are backend environment variables. No frontend env var is needed unless the frontend stores the API base URL separately.

Configure one or more gold-rate email recipients with indexed variables:

```text
GoldRate__RecipientEmails__0=first@example.com
GoldRate__RecipientEmails__1=second@example.com
```

Or with a comma-separated variable:

```text
GoldRate__RecipientEmailsCsv=first@example.com,second@example.com
```

The old single-recipient variable still works:

```text
GoldRate__RecipientEmail=first@example.com
```

If none of these are set, the API falls back to `Brevo__OwnerEmail`. Brevo still needs `Brevo__ApiKey`, `Brevo__SenderEmail`, and `Brevo__CustomEmailTemplateId`.

Recommended production setup:

```text
GoldRate__RecipientEmailsCsv=owner@example.com,accounts@example.com
GoldRate__RecipientName=Kromic Admin
GoldRate__DailyJobEnabled=true
```

## Brevo Email Templates

Ready-to-paste Brevo HTML templates are available here:

- Daily rate email: `src/docs/brevo-gold-rate-daily-template.html`
- Lowest-rate buy-now alert: `src/docs/brevo-gold-rate-lowest-alert-template.html`

Both templates use the params currently sent by the backend:

```text
{{ params.name }}
{{ params.email }}
{{ params.subject }}
{{ params.heading }}
{{ params.summary }}
{{ params.note }}
{{ params.rate1g }}
{{ params.change1g }}
{{ params.rate8g }}
{{ params.change8g }}
{{ params.changeClass }}
{{ params.fetchedAt }}
```

Current backend behavior uses `Brevo__CustomEmailTemplateId` for both gold-rate email types and sends a dedicated tabular param contract that includes the 1g and 8g rates, arrow-style differences, and the fetched date/time. If you want Brevo to use two visually separate template IDs, add separate backend config keys such as `GoldRate__DailyTemplateId` and `GoldRate__LowestAlertTemplateId`, then route each email type to the matching Brevo template.

## Endpoints

### Current Rate

Use this on the admin dashboard. Set `refresh=true` when the dashboard loads.

```http
GET /api/admin/gold-rates/current?refresh=true
Authorization: Bearer <accessToken>
```

`refresh=true` stores a new snapshot and can send the lowest-rate alert, but does not send the normal daily email.

Use `refresh=false` or omit the query string when you only want the last stored value:

```http
GET /api/admin/gold-rates/current
Authorization: Bearer <accessToken>
```

### Manual Fetch Button

Use this for the admin button. It fetches the latest source data, stores a snapshot, sends the regular email, and sends the buy-now alert when this is the lowest stored R22KT rate.

```http
POST /api/admin/gold-rates/fetch
Authorization: Bearer <accessToken>
```

### History Table

Use `range` for quick filters:

```http
GET /api/admin/gold-rates?range=1D
GET /api/admin/gold-rates?range=7D
GET /api/admin/gold-rates?range=1M
GET /api/admin/gold-rates?range=3M
GET /api/admin/gold-rates?range=6M
GET /api/admin/gold-rates?range=1Y
Authorization: Bearer <accessToken>
```

For custom date filters:

```http
GET /api/admin/gold-rates?from=2026-06-01T00:00:00Z&to=2026-06-10T23:59:59Z
Authorization: Bearer <accessToken>
```

## Response Types

```ts
export type GoldRateSnapshot = {
  id: string;
  r22KT: number;
  r22KTShow: boolean;
  r18KT: number | null;
  r24KT: number | null;
  sourceLastUpdatedAt: string | null;
  fetchedAt: string;
  isLowestAtFetch: boolean;
  regularEmailMessageId: string | null;
  lowestAlertMessageId: string | null;
};

export type GoldRateFetchResponse = {
  snapshot: GoldRateSnapshot;
  regularEmailSent: boolean;
  lowestAlertSent: boolean;
};

export type GoldRateHistoryResponse = {
  current: GoldRateSnapshot | null;
  lowest: GoldRateSnapshot | null;
  items: GoldRateSnapshot[];
};
```

The login response now also includes:

```ts
goldRate: GoldRateSnapshot | null
```

## Frontend Files To Add Or Change

Adapt names to the frontend project's structure.

### API Client

Add gold-rate client methods near the existing admin API client:

```ts
export async function getCurrentGoldRate(refresh = false): Promise<GoldRateSnapshot | null> {
  return api.get(`/api/admin/gold-rates/current${refresh ? "?refresh=true" : ""}`);
}

export async function fetchGoldRateNow(): Promise<GoldRateFetchResponse> {
  return api.post("/api/admin/gold-rates/fetch");
}

export async function getGoldRateHistory(params: {
  range?: "1D" | "7D" | "1M" | "3M" | "6M" | "1Y";
  from?: string;
  to?: string;
}): Promise<GoldRateHistoryResponse> {
  const search = new URLSearchParams();
  if (params.range) search.set("range", params.range);
  if (params.from) search.set("from", params.from);
  if (params.to) search.set("to", params.to);
  return api.get(`/api/admin/gold-rates?${search.toString()}`);
}
```

The `api` helper above should be the existing authenticated HTTP client that adds:

```http
Authorization: Bearer <accessToken>
```

### Auth Store / Login Handling

Update the login response type to include `goldRate`.

When login succeeds:

- Store the token exactly as today.
- Store `response.goldRate` in dashboard state if available.
- Do not block login UI if `goldRate` is null.

The backend already attempts to fetch the current rate during login. If the external gold-rate API is down, login still succeeds and returns the last saved rate if one exists.

### Dashboard Card

Add a compact dashboard card:

- Title: `Gold rate`
- Primary value: `R22KT`
- Secondary text: fetched timestamp in IST
- Badge when `isLowestAtFetch` is true: `Lowest saved rate`
- Loading state while refreshing
- Error state if dashboard refresh fails

Dashboard load flow:

1. Render `goldRate` from login response if present.
2. Call `GET /api/admin/gold-rates/current?refresh=true`.
3. Replace the card value with the fresh response.
4. If refresh fails, keep the previous displayed value and show a small error message.

### Manual Fetch Button

Add a button in the dashboard card or gold-rate page:

- Label: `Fetch gold rate`
- Disable while request is running.
- Call `POST /api/admin/gold-rates/fetch`.
- On success, update dashboard current rate and refresh history.
- Show normal success when `regularEmailSent` is true.
- Show stronger success when `lowestAlertSent` is true, for example: `Lowest rate found. Buy-now email sent.`

### History Page Or Section

Add a history table under admin, either on dashboard or a dedicated page.

Required controls:

- Range segmented control: `1D`, `7D`, `1M`, `3M`, `6M`, `1Y`
- Optional custom `from` and `to` date inputs
- Refresh button

When a range is selected:

```http
GET /api/admin/gold-rates?range=7D
```

When custom dates are selected:

```http
GET /api/admin/gold-rates?from=<fromIso>&to=<toIso>
```

Table columns:

- `Fetched at`
- `R22KT`
- `Source updated at`
- `Lowest`
- `Regular email`
- `Lowest alert`

Column mapping:

```ts
fetchedAt -> Fetched at
r22KT -> R22KT
sourceLastUpdatedAt -> Source updated at
isLowestAtFetch -> Lowest
regularEmailMessageId -> Regular email
lowestAlertMessageId -> Lowest alert
```

For email columns, show `Sent` when the message id is present, otherwise `Not sent`.

### Formatting Helpers

Use IST for dates:

```ts
export function formatIstDate(value: string | null): string {
  if (!value) return "-";

  return new Intl.DateTimeFormat("en-IN", {
    timeZone: "Asia/Kolkata",
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}
```

Format R22KT:

```ts
export function formatGoldRate(value: number): string {
  return value.toLocaleString("en-IN", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}
```

## Suggested Admin UI Layout

Dashboard:

- Show today's R22KT rate from the login response first if present.
- On dashboard mount, call `GET /api/admin/gold-rates/current?refresh=true` and replace the displayed value.
- Highlight the rate if `isLowestAtFetch` is true.

Manual button:

- Add a "Fetch gold rate" admin button.
- On click, call `POST /api/admin/gold-rates/fetch`.
- Show success text from `snapshot.r22KT`.
- Show a stronger success state when `lowestAlertSent` is true.

History:

- Add range tabs or segmented controls: `1D`, `7D`, `1M`, `3M`, `6M`, `1Y`.
- Call `GET /api/admin/gold-rates?range=<range>` when the selected range changes.
- Table columns: fetched date, R22KT, source updated date, lowest flag, regular email sent, lowest alert sent.

Formatting:

- Display dates in `Asia/Kolkata`.
- Display R22KT with two decimals.

## Acceptance Criteria

- Admin dashboard shows the latest R22KT rate.
- Dashboard load calls `current?refresh=true`.
- Manual fetch button calls `POST /api/admin/gold-rates/fetch`.
- Manual fetch success updates the visible current rate.
- If `lowestAlertSent` is true, the UI clearly indicates the buy-now alert was sent.
- History table loads with `7D` as the default range.
- User can switch filters between `1D`, `7D`, `1M`, `3M`, `6M`, and `1Y`.
- Dates are displayed in IST.
- API errors show non-blocking error messages.
- Expired token behavior matches the existing admin app behavior.

## QA Checklist

- Login as admin and confirm `goldRate` is handled when present.
- Login still works when `goldRate` is null.
- Open dashboard and confirm current rate refreshes once.
- Click manual fetch and confirm button loading/disabled state.
- Confirm successful manual fetch shows R22KT and email status.
- Confirm history table updates after manual fetch.
- Confirm each range filter sends the correct `range` query.
- Confirm custom `from`/`to` dates send ISO strings.
- Confirm all requests include the admin bearer token.
