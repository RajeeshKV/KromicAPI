# Brevo Contact Email Setup

This API stores every contact form submission in PostgreSQL and uses Brevo Transactional Email API to send:

- A notification email to the configured owner/admin email.
- A response email to the visitor when an admin replies.

## Brevo Account Setup

1. Log in to Brevo.
2. Go to **Transactional** and enable transactional emails if needed.
3. Verify the sender domain or sender email you want to use.
4. Go to **SMTP & API** and create an API key.
5. Create three transactional email templates:
   - Contact notification template: sent to you.
   - Contact response template: sent to the visitor.
   - Custom email template: sent from the admin custom email endpoint.
6. Copy the template IDs into the API configuration.

The API uses Brevo's v3 transactional email endpoint:

```text
POST https://api.brevo.com/v3/smtp/email
```

Authentication uses the `api-key` header.

## Environment Variables

```text
Brevo__BaseUrl=https://api.brevo.com/v3
Brevo__ApiKey=<Brevo API key>
Brevo__SenderEmail=<verified sender email>
Brevo__SenderName=Kromic
Brevo__OwnerEmail=<your personal/admin email>
Brevo__OwnerName=<your name or Kromic Admin>
Brevo__ContactNotificationTemplateId=<template id sent to owner>
Brevo__ContactResponseTemplateId=<template id sent to visitor>
Brevo__CustomEmailTemplateId=<template id used for custom admin emails>
```

## Owner Notification Template Params

Use these params in the Brevo template:

```text
{{ params.contactId }}
{{ params.name }}
{{ params.email }}
{{ params.phone }}
{{ params.subject }}
{{ params.message }}
{{ params.submittedAt }}
```

Recommended template purpose: "New contact form submission".

## Visitor Response Template Params

Use these params in the Brevo template:

```text
{{ params.contactId }}
{{ params.name }}
{{ params.email }}
{{ params.phone }}
{{ params.subject }}
{{ params.message }}
{{ params.responseText }}
{{ params.respondedAt }}
```

Recommended template purpose: "Reply from Kromic".

The API only sends `responseText`; styling, layout, branding, and footer content should live inside the Brevo template.

## Custom Email Template Params

Use these params in the Brevo template:

```text
{{ params.name }}
{{ params.email }}
{{ params.subject }}
{{ params.heading }}
{{ params.body }}
{{ params.callToActionText }}
{{ params.callToActionUrl }}
{{ params.sentAt }}
```

Recommended template purpose: "Custom message from Kromic".

Send custom emails through:

```text
POST /api/admin/emails/custom
```

The endpoint requires an admin JWT. Recipients can come from all contacted users, selected contact submission IDs, manually entered email addresses, or any combination of those sources. Duplicate email addresses are sent once.

## Notes

- `Brevo__SenderEmail` must be a verified Brevo sender.
- `Brevo__OwnerEmail` is where new contact notifications are sent.
- The contact notification email sets `replyTo` to the visitor's email, so you can reply directly from your inbox if needed.
- Admin replies should preferably go through `POST /api/admin/contacts/{id}/respond` so the response is stored in the database.
