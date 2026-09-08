# Publisher verification and code signing

Two unrelated things are both described as making the publisher "verified". They solve different
problems, are granted by different organisations, and neither is required to run PresenceLight on
your own devices in your own tenants.

| | Entra publisher verification | Code signing |
| --- | --- | --- |
| Removes | "Unverified publisher" warning on the Microsoft sign-in consent screen | "Unknown publisher" SmartScreen warning when running a downloaded build |
| Granted by | Microsoft, through the AI Cloud Partner Program | A certificate authority |
| Cost | None | Paid, ongoing |
| Needed for | Other organisations consenting to a multi-tenant app | Distributing builds to other people |

Facts below were taken from Microsoft's documentation in September 2026. Both programmes change;
confirm the current requirements before committing time or money.

## What actually applies to this project

For one or two tenants that you administer, neither is necessary. Grant admin consent during setup
and unblock the downloaded zip once per machine.

One restriction is worth understanding before relying on a multi-tenant registration. Since November
2020, where risk-based step-up consent is enabled, ordinary users cannot consent to a multi-tenant
app that is not publisher verified when all of the following hold: the app was registered after
8 November 2020, it requests permissions beyond basic sign-in and reading the user profile, and
consent is requested from a user in a tenant other than the one where the app is registered.

PresenceLight requests `Presence.Read`, which is beyond basic sign-in, so this applies to it. The
consequence for a second tenant is that an administrator must grant consent there rather than an
ordinary user consenting for themselves. That is what the *Open consent page* button in Settings is
for. Publisher verification would lift the restriction, but administrator consent avoids it.

## Entra publisher verification

Verification associates a verified Microsoft AI Cloud Partner Program account with an app
registration, and shows a blue *verified* badge on the consent prompt.

Microsoft does not charge for it, and no licence is required. The obstacles are organisational
rather than financial:

- A Partner One ID for a Microsoft AI Cloud Partner Program account that has completed verification.
  It must be the partner global account, not a partner location ID. Verification is of a legal
  business entity, so this is the step an individual cannot usually complete.
- The app must be registered using a work or school account. Apps registered with a personal
  Microsoft account cannot be publisher verified.
- The tenant holding the registration must be associated with the partner global account.
- The registration must have a publisher domain set, and **it cannot be a `*.onmicrosoft.com`
  domain**. A custom domain you own is required.
- The email domain used when verifying the partner account must match that publisher domain, or be a
  DNS-verified custom domain added to the tenant.
- The person doing it needs Application Administrator or Cloud Application Administrator in Entra ID,
  plus Partner Admin or Account Admin in Partner Center, and must sign in with multifactor
  authentication.

It is not supported in national clouds. Developers who already meet the prerequisites can complete
verification in minutes; obtaining a verified partner account and a custom domain is the slow part.

This is what stopped the upstream project. Its author reported in
[issue 978](https://github.com/isaacrlevin/presencelight/issues/978) that he no longer holds partner
status, which is why the shared registration could not be recreated outside Microsoft's tenant.

## Code signing

Windows warns about software from an unknown publisher because the build is unsigned. Removing the
warning requires a code signing certificate. Since June 2023 the private key must be held in
certified hardware or a cloud service, so inexpensive file-based certificates no longer exist.

### Azure Artifact Signing

Formerly Trusted Signing, this is Microsoft's managed signing service. Keys live in FIPS 140-3
level 3 hardware and certificate lifecycle is managed for you, which avoids buying and storing a
certificate yourself.

- Two tiers. Basic includes 5,000 signatures a month and one certificate profile of each type;
  Premium includes 100,000 signatures and ten profiles. The public pricing page does not render
  figures without a region, so check the cost in the portal when creating the account.
- **Public Trust certificates for individual developers are limited to the United States and
  Canada.** For organisations the list is wider: the United States, Canada, the European Union, the
  United Kingdom, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway and
  Israel. Private Trust certificates have no such geographic restriction, but they are not publicly
  trusted, so they do not solve the SmartScreen warning for other people.
- Individual validation reads its details from the Azure billing account, whose type must be
  *Individual*, and the name and address must match your government-issued identification exactly.
  Identity is then proven through a third-party verifier using photo identification and a Verified ID
  in Microsoft Authenticator.
- Public identity validation takes between 1 and 20 business days, longer if more documents are
  requested.

### Certificate from a certificate authority

Buying an OV or EV certificate directly remains possible. Organisation validated certificates
generally require a registered business, and reputation with SmartScreen accrues over time;
extended validation certificates are trusted by SmartScreen sooner but cost more. Either way the key
must be held on a hardware token or in a cloud key store.

### Wiring it into this repository

The signing pipeline already exists. [Sign.yml](../.github/workflows/Sign.yml) installs the `sign`
CLI tool and signs using a certificate in Azure Key Vault, identified by the
`KEY_VAULT_CERTIFICATE_ID` secret. Once a certificate exists, signing the artifact build is a matter
of supplying that secret and calling the workflow after the publish step, rather than new
development.

### The free alternative

For a small number of machines you control, a self-signed certificate installed into the Trusted
Publishers store on those machines removes the warning at no cost. It carries no weight anywhere
else, so it suits personal use and nothing wider. Simply unblocking the downloaded zip through its
file properties achieves the same result with less effort.
