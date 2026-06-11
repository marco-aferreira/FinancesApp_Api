from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.colors import HexColor, white
from reportlab.lib.units import inch
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    PageBreak, HRFlowable, Preformatted, KeepTogether
)
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

BLUE_DARK  = HexColor('#1F4E79')
BLUE_MID   = HexColor('#2E75B6')
BLUE_LIGHT = HexColor('#DEEAF1')
BLUE_PALE  = HexColor('#F2F7FB')

W, H = letter

def make_styles():
    styles = getSampleStyleSheet()

    def add(name, **kw):
        styles.add(ParagraphStyle(name=name, **kw))

    add('DocTitle',    fontSize=28, leading=34, textColor=BLUE_DARK,  alignment=TA_CENTER, fontName='Helvetica-Bold', spaceAfter=4)
    add('DocSubtitle', fontSize=14, leading=18, textColor=BLUE_DARK,  alignment=TA_CENTER, fontName='Helvetica',      spaceAfter=6)
    add('DocMeta',     fontSize=11, leading=14, textColor=BLUE_DARK,  alignment=TA_CENTER, fontName='Helvetica',      spaceAfter=20)
    add('SectionHead', fontSize=18, leading=22, textColor=BLUE_DARK,  fontName='Helvetica-Bold', spaceBefore=14, spaceAfter=6)
    add('SubHead',     fontSize=11, leading=14, textColor=BLUE_MID,   fontName='Helvetica-Bold', spaceBefore=8,  spaceAfter=4)
    add('Body',        fontSize=9,  leading=13, textColor=HexColor('#222222'), fontName='Helvetica', spaceAfter=4)
    add('BodySmall',   fontSize=8,  leading=12, textColor=HexColor('#222222'), fontName='Helvetica', spaceAfter=4)
    add('Note',        fontSize=8,  leading=12, textColor=BLUE_MID,   fontName='Helvetica-Oblique', spaceAfter=6)
    add('CodePre',     fontSize=7.5,leading=11, textColor=HexColor('#1a1a1a'), fontName='Courier',  spaceAfter=6,
        leftIndent=12, backColor=HexColor('#F5F5F5'), borderPadding=4)
    add('Footer',      fontSize=7.5,leading=10, textColor=BLUE_MID,   alignment=TA_CENTER, fontName='Helvetica')
    add('BulletBody',  fontSize=9,  leading=13, textColor=HexColor('#222222'), fontName='Helvetica',
        leftIndent=12, bulletIndent=0, spaceAfter=3)
    return styles

S = make_styles()

def section(title):
    return [
        HRFlowable(width='100%', thickness=1.5, color=BLUE_DARK, spaceAfter=4),
        Paragraph(title, S['SectionHead']),
    ]

def sub(title):
    return Paragraph(title, S['SubHead'])

def body(text):
    return Paragraph(text, S['Body'])

def note(text):
    return Paragraph(text, S['Note'])

def code(text):
    return Preformatted(text, S['CodePre'])

def bullet(text):
    return Paragraph(f'- {text}', S['BulletBody'])

def tbl(data, col_widths, header=True):
    t = Table(data, colWidths=col_widths, repeatRows=1 if header else 0)
    cmds = [
        ('FONTNAME',    (0,0), (-1,-1), 'Helvetica'),
        ('FONTSIZE',    (0,0), (-1,-1), 8),
        ('LEADING',     (0,0), (-1,-1), 11),
        ('VALIGN',      (0,0), (-1,-1), 'TOP'),
        ('TOPPADDING',  (0,0), (-1,-1), 4),
        ('BOTTOMPADDING',(0,0),(-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING',(0,0), (-1,-1), 6),
        ('GRID',        (0,0), (-1,-1), 0.5, HexColor('#B0C4D8')),
    ]
    if header:
        cmds += [
            ('BACKGROUND', (0,0), (-1,0), BLUE_MID),
            ('TEXTCOLOR',  (0,0), (-1,0), white),
            ('FONTNAME',   (0,0), (-1,0), 'Helvetica-Bold'),
        ]
        for i in range(1, len(data)):
            bg = BLUE_LIGHT if i % 2 == 1 else BLUE_PALE
            cmds.append(('BACKGROUND', (0,i), (-1,i), bg))
    t.setStyle(TableStyle(cmds))
    return t

def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont('Helvetica', 7.5)
    canvas.setFillColor(BLUE_MID)
    txt = f'FinancesApp API - Frontend Development Guide | Page {doc.page}'
    canvas.drawCentredString(W / 2, 0.4 * inch, txt)
    canvas.setStrokeColor(BLUE_MID)
    canvas.setLineWidth(0.5)
    canvas.line(inch, 0.55 * inch, W - inch, 0.55 * inch)
    canvas.restoreState()

def build():
    out = 'FinancesApp_API_Frontend_Guide.pdf'
    doc = SimpleDocTemplate(
        out,
        pagesize=letter,
        leftMargin=inch, rightMargin=inch,
        topMargin=0.8*inch, bottomMargin=0.8*inch,
    )

    cw_2col = [2.8*inch, 4.2*inch]
    cw_2col_wide = [2.0*inch, 5.0*inch]
    cw_status = [0.7*inch, 6.3*inch]
    cw_4col  = [1.2*inch, 1.5*inch, 1.5*inch, 2.8*inch]

    story = []

    # ─────────────────────────────────────────────
    # PAGE 1 — Title
    # ─────────────────────────────────────────────
    story += [
        Spacer(1, 1.8*inch),
        Paragraph('FinancesApp API', S['DocTitle']),
        Paragraph('Frontend Development Guide', S['DocSubtitle']),
        Spacer(1, 0.15*inch),
        Paragraph('API Reference for Building the FinancesApp Frontend', S['DocMeta']),
        Paragraph('Version 1.1 | Generated May 2026', S['DocMeta']),
        Spacer(1, 0.5*inch),
        HRFlowable(width='60%', thickness=1, color=BLUE_MID, hAlign='CENTER', spaceAfter=16),
        sub('Architecture Overview'),
        body('Frontend (SPA) → FinancesApp_Api (Controllers, JWT) → Modules: User / Credentials / Account CQRS '
             'Dispatcher + Event Store → Outbox Processor (BackgroundService) → Projections (read tables) '
             'SQL Server (events + reads) | AWS S3 (profile images) | DynamoDB (image metadata) | Redis (cache)'),
        PageBreak(),
    ]

    # ─────────────────────────────────────────────
    # PAGE 2 — What's new + Sections 1 & 2
    # ─────────────────────────────────────────────
    story += section("What's new in v1.1")
    story.append(body('v1.1 is fully backward-compatible with v1.0. Additions:'))
    story.append(tbl([
        ['Endpoint', 'Description'],
        ['GET /api/v1.1/accounts/transactions',
         'Cross-account transaction history. Optional from/to (ISO 8601). Sorted newest-first.'],
        ['GET /api/v1.1/user/get/{userId}',
         'Returns { user, profileImageUrl } — a 1-minute pre-signed S3 GET URL, or null if no image.'],
        ['POST /api/v1.1/credentials/verify-2fa (updated)',
         'Full JWT now delivered as HttpOnly cookie (X-Access-Token, SameSite=Strict). '
         'Response body contains only { profileImageUrl }.'],
        ['POST /api/v1.1/credentials/logout (new)',
         'Authenticated via X-Access-Token cookie. Invalidates any active TOTP for the user '
         'and clears X-Access-Token + X-Username cookies.'],
    ], cw_2col))
    story.append(body('Versioning rule: minor bumps 1.0 → 1.1 → ... → 1.9, then rolls to next major (2.0).'))
    story.append(Spacer(1, 0.15*inch))

    story += section('1. What this API is about')
    story.append(body(
        'FinancesApp is a personal-finance management API. JWT authentication via HttpOnly cookies '
        '(set automatically after 2FA) and Bearer token for the partial-auth step. '
        '.NET 8 modular monolith — three domains:'
    ))
    story.append(tbl([
        ['Domain', 'Responsibility'],
        ['User',        'Profile data: name, email, date of birth, profile image (stored in S3).'],
        ['Credentials', 'Login + password (BCrypt) and TOTP-based two-step authentication.'],
        ['Account',     'Financial accounts (Cash, Checking, Credit Card) with balance, debt, deposits, '
                        'withdrawals, payments/purchases. Each change is an immutable event.'],
    ], cw_2col))
    story.append(body(
        '<b>Eventual consistency:</b> every state change is an immutable event. Read endpoints serve '
        'projections updated asynchronously (~500ms). Re-fetch after writes where freshness matters.'
    ))
    story.append(Spacer(1, 0.12*inch))

    story += section('2. Base URL and API versioning')
    story.append(tbl([
        ['Method',       'Example'],
        ['URL segment',  '/api/v1/... or /api/v1.1/... (recommended)'],
        ['Media type',   'Accept: application/json;api-version=1.1'],
    ], cw_2col))
    story.append(body(
        'Default version is <b>v1.1</b>. CORS allows any localhost/127.0.0.1 origin for development. '
        'HTTPS redirection is skipped in Development.'
    ))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 3 — Section 3: Authentication
    # ─────────────────────────────────────────────
    story += section('3. Authentication (two-step, TOTP-based)')
    story.append(body(
        'Login is a two-step flow. Step 1 verifies the password → <b>partial JWT</b> + QR code. '
        'Step 2 verifies the TOTP code → full JWT delivered as an <b>HttpOnly cookie</b> + profile image URL.'
    ))
    story.append(Spacer(1, 0.08*inch))

    story.append(sub('Step 1 — POST /api/v1.1/credentials/login'))
    story.append(body(
        'Send { login, password }. Returns { token: &lt;partialJwt&gt;, qrCodeImage: "data:image/png;base64,..." }. '
        'Show the QR code so the user can scan it with an authenticator app.'
    ))

    story.append(sub('Step 2 — POST /api/v1.1/credentials/verify-2fa'))
    story.append(body(
        'Send { totpCode } with <font face="Courier">Authorization: Bearer &lt;partialToken&gt;</font>. '
        'Returns <b>only { profileImageUrl }</b> in the response body. '
        'The full JWT and username are delivered as <b>HttpOnly cookies</b> set by the server:'
    ))
    story.append(tbl([
        ['Cookie',         'Value',          'Flags'],
        ['X-Access-Token', 'Full JWT string', 'HttpOnly, SameSite=Strict'],
        ['X-Username',     'User display name', 'HttpOnly, SameSite=Strict'],
    ], [2.0*inch, 2.2*inch, 2.8*inch]))
    story.append(body(
        'Cookies are sent automatically by the browser on all subsequent same-origin requests. '
        'For cross-origin, add <font face="Courier">credentials: \'include\'</font> to your fetch options.'
    ))
    story.append(code(
        '// verify-2fa 200 response body\n'
        '{\n'
        '  "profileImageUrl": "https://s3.amazonaws.com/...?X-Amz-..."  // null if no image uploaded\n'
        '}'
    ))
    story.append(body(
        '<b>profileImageUrl</b> is a pre-signed S3 GET URL valid for <b>1 minute</b>. Use it immediately '
        'to display the avatar right after login. After 1 minute, call '
        '<font face="Courier">GET /user/get/{userId}</font> for a fresh URL.'
    ))

    story.append(sub('Full JWT claims'))
    story.append(tbl([
        ['Claim',                             'Value'],
        ['Issuer',                            'https://FinancesApp.com'],
        ['Audience',                          'https://FinancesAppCustomers.com'],
        ['Algorithm',                         'RS256'],
        ['token_type',                        '"partial" after login, "full" after verify-2fa'],
        ['userid_enc',                        'AES-encrypted user GUID (do not parse client-side)'],
        ['role / 2fa_verified / AccountIds',  'Present only on the full token after successful TOTP verification.'],
    ], cw_2col_wide))

    story.append(note(
        'Note: profile image URL is not a JWT claim. It is returned as a separate top-level field in '
        'the verify-2fa response body. This avoids embedding a short-lived URL inside a longer-lived token.'
    ))

    story.append(sub('Important rules'))
    story += [
        bullet('TOTP codes are <b>one-time</b>: invalidated after successful verify. New login → new QR code (re-pair each session).'),
        bullet('TOTP secrets expire after ~5 minutes. If too slow, restart from /login.'),
        bullet('The <font face="Courier">x-api-key</font> header is an additional filter on selected endpoints. Not a replacement for JWT auth.'),
        bullet('The full JWT is in an HttpOnly cookie — JavaScript cannot read it. Do not try to decode it client-side.'),
    ]
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 4 — Rate limiting
    # ─────────────────────────────────────────────
    story += section('4. Rate limiting')
    story.append(body(
        'Sliding-window limits enforced server-side. Exceeding returns <b>429 Too Many Requests</b> with '
        '<font face="Courier">Retry-After</font> header.'
    ))
    story.append(tbl([
        ['Policy',      'Limit',                      'Partition key',    'Where it applies'],
        ['global',      '100 req/min + 10 concurrent', 'IP / user',       'every endpoint'],
        ['auth',        '10 req / 30s',                'IP',              'POST /credentials/login'],
        ['verify-totp', '5 req / 30s',                 'userId (from JWT)','POST /credentials/verify-2fa'],
        ['delta',       '30 req / 30s',                'userId (from JWT)','POST /accounts/delta'],
    ], cw_4col))
    story.append(body(
        '<b>Tip:</b> on 429 read <font face="Courier">Retry-After</font> and disable the button. '
        'For verify-2fa, surface a clear message — users retype codes quickly.'
    ))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 5 — Shared types
    # ─────────────────────────────────────────────
    story += section('5. Shared types and conventions')
    story.append(sub('Money (value object)'))
    story.append(code(
        '{\n'
        '  "amount": 1500.50,\n'
        '  "currency": "BRL"\n'
        '}'
    ))
    story += [
        bullet('<font face="Courier">currency</font>: 3-letter ISO 4217 code (BRL, USD, EUR, ...).'),
        bullet('<font face="Courier">amount</font>: rounded to 2 decimals (MidpointRounding.AwayFromZero).'),
        bullet('All arithmetic is same-currency; no auto-conversion.'),
    ]
    story.append(Spacer(1, 0.08*inch))
    story.append(sub('Enumerations'))
    story.append(tbl([
        ['Enum',                   'Values'],
        ['AccountType',            '0 = Cash, 1 = Checking, 2 = CreditCard'],
        ['AccountStatus',          '0 = Active, 1 = Closed'],
        ['OperationType',          '0 = MoneyTransaction (deposit/withdraw), 1 = Payment, 2 = CreditPurchase'],
        ['TransactionKind (v1.1)', '0 = Deposit, 1 = Withdraw, 2 = CreditCardPayment, 3 = CreditCardChange'],
    ], cw_2col))
    story.append(sub('General conventions'))
    story += [
        bullet('IDs are GUIDs (UUID v4 strings).'),
        bullet('Dates use ISO 8601 with offset, e.g. <font face="Courier">2026-04-08T12:30:00+00:00</font>.'),
        bullet('400 errors return a plain string body. 404 returns no body.'),
        bullet('List endpoints return [] (not 404) when empty.'),
        bullet('Write responses are eventually consistent: re-read after ~1s.'),
    ]
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 6-7 — User endpoints
    # ─────────────────────────────────────────────
    story += section('6. User endpoints')
    story.append(body('<b>Base route:</b> <font face="Courier">/api/v1.1/user</font>  <b>Auth:</b> not required.'))
    story.append(Spacer(1, 0.06*inch))

    story.append(sub('GET /api/v1.1/user'))
    story.append(body('Get all users'))
    story.append(tbl([
        ['Auth',   'None'],
        ['Status', 'Body / Notes'],
        ['200',    'Array of User objects'],
        ['200',    'Returns [] when no users exist'],
    ], cw_2col, header=False))

    story.append(sub('User object schema'))
    story.append(code(
        '[\n'
        '  {\n'
        '    "id":             "550e8400-e29b-41d4-a716-446655440000",\n'
        '    "name":           "John Doe",\n'
        '    "email":          "john@example.com",\n'
        '    "registeredAt":   "2026-01-15T00:00:00+00:00",\n'
        '    "modifiedAt":     "2026-01-15T00:00:00+00:00",\n'
        '    "dateOfBirth":    "1990-05-20T00:00:00+00:00",\n'
        '    "profileImage":   "profile-images/{userId}/profile",\n'
        '    "isDeleted":      false,\n'
        '    "age":            35\n'
        '  }\n'
        ']'
    ))

    story.append(sub('GET /api/v1.1/user/get/{userId}'))
    story.append(body('Get a single user by id — includes a fresh pre-signed profile image URL'))
    story.append(tbl([
        ['Auth',    'None'],
        ['userId',  'GUID string'],
        ['Status',  'Body / Notes'],
        ['200',     '{ "user": { ...User object... }, "profileImageUrl": "https://s3.amazonaws.com/...?X-Amz-..." }'],
        ['200',     'profileImageUrl is null if the user has no profile image'],
        ['400',     '"Invalid Id"'],
        ['404',     'User not found'],
    ], cw_2col, header=False))
    story.append(note(
        '- profileImageUrl is valid for 1 minute. Use it immediately; do not cache across sessions.'
    ))

    story.append(sub('POST /api/v1.1/user/create'))
    story.append(body('Create a new user'))
    story.append(tbl([['Auth', 'None']], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "name":        "John Doe",\n'
        '  "email":       "john@example.com",\n'
        '  "dateOfBirth": "1990-05-20T00:00:00+00:00",\n'
        '  "profileImage": ""\n'
        '}'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    'GUID of the created user'],
        ['400',    'Validation failed (name <=100, email contains @ and <=50, age 16-120)'],
    ], cw_status))

    story.append(sub('PUT /api/v1.1/user/update/{userId}'))
    story.append(body('Update user — name/email are optional; omit or send empty to keep existing values'))
    story.append(tbl([['Auth', 'None']], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "id":           "550e8400-...",\n'
        '  "name":         "John Updated",\n'
        '  "email":        "john.new@example.com",\n'
        '  "dateOfBirth":  "1990-05-20T00:00:00+00:00",\n'
        '  "profileImage": "<base64-encoded image or empty string>"\n'
        '}'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    'GUID of the updated user'],
        ['400',    'Update failed'],
    ], cw_status))
    story.append(note(
        'Profile image upload: base64-encoded JPEG/PNG/WebP, max 2 MB. Processed asynchronously — '
        'updated URL appears in GET /user/get/{userId} after ~500ms+. Send empty string to leave existing image unchanged.'
    ))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 8-10 — Credentials endpoints
    # ─────────────────────────────────────────────
    story += section('7. Credentials endpoints')
    story.append(body(
        '<b>Base route:</b> <font face="Courier">/api/v1.1/credentials</font>  '
        '<b>Auth:</b> mostly none; verify-2fa needs a partial token.'
    ))

    story.append(sub('GET /api/v1.1/credentials/user/{userId}'))
    story.append(body('Look up credentials by user id (no password returned)'))
    story.append(tbl([
        ['Auth',   'None'],
        ['userId', 'GUID string'],
        ['Status', 'Body / Notes'],
        ['200',    'UserCredentials object'],
        ['400',    '"Invalid Id"'],
        ['404',    'Not found'],
    ], cw_2col, header=False))

    story.append(sub('GET /api/v1.1/credentials/login/{login}'))
    story.append(body('Look up credentials by email (no password returned)'))
    story.append(tbl([
        ['Auth',   'None'],
        ['login',  'email string, non-empty'],
        ['Status', 'Body / Notes'],
        ['200',    'UserCredentials object'],
        ['400',    '"Login cannot be empty"'],
        ['404',    'Not found'],
    ], cw_2col, header=False))

    story.append(sub('POST /api/v1.1/credentials/login'))
    story.append(body('Step 1: verify password, generate TOTP secret, return partial JWT + QR code'))
    story.append(tbl([
        ['Auth',       'None'],
        ['Rate limit', '10 req / 30s per IP'],
    ], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "login":         "john@example.com",\n'
        '  "plainPassword": "SecurePass1!"\n'
        '}'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    '{ "token": "<partialJwt>", "qrCodeImage": "data:image/png;base64,..." }'],
        ['401',    'Invalid credentials'],
        ['500',    'TOTP secret could not be persisted'],
    ], cw_status))
    story.append(PageBreak())

    story.append(sub('POST /api/v1.1/credentials/verify-2fa'))
    story.append(body('Step 2: verify 6-digit TOTP, set full JWT as HttpOnly cookie, return profile image URL'))
    story.append(tbl([
        ['Auth',       'Authorization: Bearer <partialToken>'],
        ['Rate limit', '5 req / 30s per user'],
    ], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "totpCode": "123456"\n'
        '}'
    ))
    story.append(body('<b>200 Response body</b>'))
    story.append(code(
        '{\n'
        '  "profileImageUrl": "https://s3.amazonaws.com/bucket/profile-images/{userId}/profile?X-Amz-Algorithm=..."\n'
        '  // null if the user has no profile image uploaded\n'
        '}'
    ))
    story.append(body('<b>200 Cookies set</b>'))
    story.append(tbl([
        ['Cookie',         'Content',           'Flags'],
        ['X-Access-Token', 'Full JWT',           'HttpOnly, SameSite=Strict'],
        ['X-Username',     'User display name',  'HttpOnly, SameSite=Strict'],
    ], [2.0*inch, 2.0*inch, 3.0*inch]))
    story.append(note(
        'profileImageUrl is a direct S3 HTTPS URL — use it as an image src with no extra headers. '
        'It expires in 1 minute. After expiry, call GET /user/get/{userId} for a fresh one.'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    '{ "profileImageUrl": "..." }  +  X-Access-Token + X-Username cookies'],
        ['400',    'Invalid code format (must be 6 digits)'],
        ['401',    'Not partial / TOTP expired / wrong code / no active TOTP'],
        ['404',    'User credentials not found'],
    ], cw_status))

    story.append(sub('POST /api/v1.1/credentials/logout  NEW in 1.1'))
    story.append(body(
        'Sign the user out. Reads the JWT from the <font face="Courier">X-Access-Token</font> HttpOnly '
        'cookie, invalidates any active TOTP for the user (server-side, event-sourced), and clears '
        'both auth cookies.'
    ))
    story.append(tbl([
        ['Auth',   'Cookie: X-Access-Token (full or partial JWT)'],
        ['Body',   'None'],
        ['Status', 'Body / Notes'],
        ['200',    'Empty body. X-Access-Token and X-Username cookies are cleared (expired).'],
        ['401',    'Token missing or missing user identity claim.'],
        ['400',    'Invalid UserId in token.'],
    ], cw_2col, header=False))
    story.append(note(
        'UserId is read from the encrypted userid_enc claim — no body needed. After the call returns 200, '
        'the browser drops the auth cookies automatically; redirect to /login.'
    ))
    story.append(code(
        '// Logout — no body, cookie carries the token\n'
        'await fetch(`${BASE_URL}/api/v1.1/credentials/logout`, {\n'
        '  method:      "POST",\n'
        '  credentials: "include",  // sends X-Access-Token\n'
        '});\n'
        '// Cookies are cleared — route the user to /login\n'
        'redirectToLogin();'
    ))

    story.append(sub('POST /api/v1.1/credentials'))
    story.append(body('Create credentials for an existing user (lookup by email)'))
    story.append(tbl([['Auth', 'None']], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "email":         "john@example.com",\n'
        '  "plainPassword": "SecurePass1!"\n'
        '}'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    'GUID of the created credentials'],
        ['400',    '"Failed to create credentials"'],
        ['404',    'No user with that email — create the user first'],
    ], cw_status))
    story.append(note('Password: min 8 chars, stored as BCrypt hash. Never echoed back.'))
    story.append(PageBreak())

    story.append(sub('PUT /api/v1.1/credentials/{userId}'))
    story.append(body('Update password'))
    story.append(tbl([['Auth', 'None']], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "userId":           "550e8400-...",\n'
        '  "newPlainPassword": "NewSecurePass1!"\n'
        '}'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    '"Credentials updated successfully"'],
        ['400',    '"Invalid Id" or "Failed to update credentials"'],
    ], cw_status))

    story.append(sub('DELETE /api/v1.1/credentials/{userId}'))
    story.append(body('Delete credentials'))
    story.append(tbl([
        ['Auth',   'None'],
        ['userId', 'GUID string'],
        ['Status', 'Body / Notes'],
        ['200',    '"Credentials deleted successfully"'],
        ['400',    '"Invalid Id" or "Failed to delete credentials"'],
    ], cw_2col, header=False))

    story.append(sub('POST /api/v1.1/credentials/rebuild-projection/{userId}'))
    story.append(body('Rebuild credentials read model from event store (admin / recovery only)'))
    story.append(tbl([
        ['Auth',   'None (ops use)'],
        ['userId', 'GUID string'],
        ['Status', 'Body / Notes'],
        ['200',    '"Projection rebuilt for user <guid>"'],
        ['400',    'Rebuild failed'],
    ], cw_2col, header=False))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGES 11-12 — Account endpoints
    # ─────────────────────────────────────────────
    story += section('8. Account endpoints')
    story.append(body(
        '<b>Base route:</b> <font face="Courier">/api/v1.1/accounts</font>  '
        '<b>Auth:</b> Bearer JWT required (sent automatically via X-Access-Token cookie). '
        'GetAccounts and GetTransactionHistory require the <i>full</i> token (post-2FA).'
    ))

    story.append(sub('Account object schema'))
    story.append(code(
        '{\n'
        '  "id":           "550e8400-...",\n'
        '  "userId":       "660e8400-...",\n'
        '  "balance":      { "amount": 1500.50, "currency": "BRL" },\n'
        '  "creditLimit":  { "amount": 3000.00, "currency": "BRL" },\n'
        '  "currentDebt":  { "amount":    0.00, "currency": "BRL" },\n'
        '  "paymentDate":  null, "dueDate": null, "payedAt": null,\n'
        '  "status":       0,  // 0=Active, 1=Closed\n'
        '  "type":         0,  // 0=Cash, 1=Checking, 2=CreditCard\n'
        '  "createdAt":    "2026-01-15T00:00:00+00:00",\n'
        '  "closedAt":     null,\n'
        '  "updatedAt":    "2026-04-08T12:00:00+00:00"\n'
        '}'
    ))
    story.append(note('creditLimit/currentDebt/paymentDate/dueDate/payedAt only meaningful on CreditCard (type=2).'))

    story.append(sub('GET /api/v1.1/accounts'))
    story.append(body("List the caller's accounts"))
    story.append(tbl([
        ['Auth',   'Cookie: X-Access-Token (full JWT)'],
        ['Status', 'Body / Notes'],
        ['200',    'Array of Account objects'],
        ['401',    'Token missing / not a full token'],
    ], cw_2col, header=False))
    story.append(note(
        'UserId is taken from the encrypted userid_enc claim — do not pass it in the body or query string.'
    ))

    story.append(sub('GET /api/v1.1/accounts/{accountId}'))
    story.append(body('Get a single account by id'))
    story.append(tbl([
        ['Auth',      'Cookie: X-Access-Token'],
        ['accountId', 'Account GUID'],
        ['Status',    'Body / Notes'],
        ['200',       'Account object'],
        ['400',       '"Invalid Id"'],
        ['404',       'Account not found'],
    ], cw_2col, header=False))

    story.append(sub('GET /api/v1.1/accounts/active'))
    story.append(body('Active accounts only'))
    story.append(tbl([
        ['Auth',   'Cookie: X-Access-Token'],
        ['Status', 'Body / Notes'],
        ['200',    'Array of Account objects (status = Active)'],
    ], cw_2col, header=False))

    story.append(sub('POST /api/v1.1/accounts'))
    story.append(body('Create a new account'))
    story.append(tbl([['Auth', 'Cookie: X-Access-Token']], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "userId":  "660e8400-...",\n'
        '  "balance": { "amount": 1000.00, "currency": "BRL" },\n'
        '  "type":    0\n'
        '}'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    '"Account created successfully"'],
        ['400',    '"Failed to create account"'],
    ], cw_status))

    story.append(sub('POST /api/v1.1/accounts/delta'))
    story.append(body('Apply a financial transaction (deposit, withdraw, payment, purchase)'))
    story.append(tbl([
        ['Auth',       'Cookie: X-Access-Token'],
        ['Rate limit', '30 req / 30s per user'],
    ], cw_2col, header=False))
    story.append(body('<b>Request body</b>'))
    story.append(code(
        '{\n'
        '  "userId":        "660e8400-...",\n'
        '  "accountId":     "550e8400-...",\n'
        '  "amount":        250.75,\n'
        '  "currency":      "BRL",\n'
        '  "operationType": 0,\n'
        '  "requestedAt":   "2026-04-08T12:30:00+00:00"\n'
        '}'
    ))
    story.append(tbl([
        ['Status', 'Body / Notes'],
        ['200',    '"Delta applied successfully"'],
        ['400',    'Business-rule failure (insufficient funds, currency mismatch, closed account, etc.)'],
        ['404',    'Account not found'],
    ], cw_status))
    story.append(body(
        'MoneyTransaction(0): positive=deposit, negative=withdrawal (Cash/Checking). '
        'Payment(1): reduces credit card debt. CreditPurchase(2): increases debt. '
        'requestedAt optional — server stamps UTC now when omitted.'
    ))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 13 — Transaction history
    # ─────────────────────────────────────────────
    story += section('8.1 Transaction history (new in v1.1)')
    story.append(body("Unified time-ordered feed across all of the authenticated user's accounts."))

    story.append(sub('GET /api/v1.1/accounts/transactions  NEW in 1.1'))
    story.append(tbl([
        ['Since',  'v1.1'],
        ['Auth',   'Cookie: X-Access-Token (full JWT)'],
        ['from',   'Optional. ISO 8601 datetime. Lower bound (inclusive).'],
        ['to',     'Optional. ISO 8601 datetime. Upper bound (inclusive).'],
        ['Status', 'Body / Notes'],
        ['200',    'Array of AccountTransaction objects, newest-first'],
        ['400',    '"from" must be earlier than or equal to "to".'],
        ['401',    'Token missing / not a full token'],
    ], cw_2col, header=False))

    story.append(sub('AccountTransaction schema'))
    story.append(code(
        '[\n'
        '  {\n'
        '    "eventId":   "11111111-...",\n'
        '    "accountId": "550e8400-...",\n'
        '    "timestamp": "2026-04-08T12:30:00+00:00",\n'
        '    "kind":      0,\n'
        '    "amount":    { "amount": 250.75, "currency": "BRL" }\n'
        '  }\n'
        ']'
    ))
    story.append(tbl([
        ['Kind',                  'Meaning'],
        ['0 - Deposit',           'Money in to Cash/Checking. amount is positive.'],
        ['1 - Withdraw',          'Money out of Cash/Checking. amount is positive (magnitude).'],
        ['2 - CreditCardPayment', 'Statement payment on a Credit Card (raised via PayCreditCardDebt).'],
        ['3 - CreditCardChange',  'Debt change from ApplyDelta on a credit card. amount = NewDebt - CurrentDebt: positive=purchase, negative=payment.'],
    ], [1.5*inch, 5.5*inch]))
    story.append(body(
        'Reflects the event store in real time — newer than projection-based Account read endpoints. '
        'O(accounts x events); query narrower windows for long-lived users.'
    ))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 14 — Health check
    # ─────────────────────────────────────────────
    story += section('9. Health check')
    story.append(sub('GET /health'))
    story.append(body('Service liveness + infrastructure probes'))
    story.append(tbl([
        ['Auth',   'None'],
        ['Status', 'Body / Notes'],
        ['200',    'JSON describing each registered health check'],
    ], cw_2col, header=False))
    story.append(code(
        '{\n'
        '  "status":   "Healthy",\n'
        '  "duration": "00:00:00.123",\n'
        '  "checks": [\n'
        '    { "name": "SQL Database Check", "status": "Healthy", "tags": ["database","critical"] },\n'
        '    { "name": "S3 Bucket Check",    "status": "Healthy", "tags": ["aws","storage"] },\n'
        '    { "name": "DynamoDB Check",     "status": "Healthy", "tags": ["aws","storage"] }\n'
        '  ]\n'
        '}'
    ))
    story.append(note(
        'All three checks run on every /health poll. S3 uses GetBucketLocation; '
        'DynamoDB uses DescribeTable (returns Degraded if table is not ACTIVE).'
    ))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 15 — Profile image guide
    # ─────────────────────────────────────────────
    story += section('10. Profile image guide')
    story.append(body(
        'Profile images are stored in AWS S3. The frontend never holds credentials — '
        'all access is via short-lived pre-signed URLs.'
    ))

    story.append(sub('Upload a profile image'))
    story.append(body(
        'Send the image as base64 in <font face="Courier">profileImage</font> field of '
        '<font face="Courier">PUT /api/v1.1/user/update/{userId}</font>. '
        'Accepted: JPEG, PNG, WebP, max 2 MB. Upload is async — updated URL appears in '
        'GET /user/get/{userId} after ~500ms+.'
    ))

    story.append(sub('Get the pre-signed URL'))
    story.append(body('Two ways to get the URL — both return the same S3 pre-signed URL, valid for 1 minute:'))
    story.append(tbl([
        ['Source',                                         'When to use'],
        ['POST /credentials/verify-2fa → profileImageUrl', 'Immediately after login. Use the URL from the response body to display the avatar right on the post-login screen.'],
        ['GET /user/get/{userId} → profileImageUrl',       'Any time after login, or after the 1-minute window expires. Always returns a fresh URL.'],
    ], [2.5*inch, 4.5*inch]))

    story.append(sub('Display a profile image'))
    story.append(code(
        '// After verify-2fa — response body contains only profileImageUrl:\n'
        'const { profileImageUrl } = await verifyTotpResponse.json();\n'
        'if (profileImageUrl) imgElement.src = profileImageUrl;\n'
        '\n'
        '// After expiry — refresh:\n'
        'const { user, profileImageUrl: freshUrl } =\n'
        '  await (await api(`/api/v1.1/user/get/${userId}`)).json();\n'
        'if (freshUrl) imgElement.src = freshUrl;'
    ))
    story.append(body('The URL is a plain HTTPS GET — no extra headers required.'))
    story.append(body(
        '<b>Important:</b> pre-signed URLs expire in <b>1 minute</b>. Do not cache across sessions or '
        'store in persistent state. Each upload overwrites the previous image '
        '(fixed key: <font face="Courier">profile-images/{userId}/profile</font>).'
    ))
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 16 — Frontend implementation guidance
    # ─────────────────────────────────────────────
    story += section('11. Frontend implementation guidance')
    story.append(sub('Recommended user flow'))
    story += [
        bullet('Sign-up: <font face="Courier">POST /user/create</font>, then <font face="Courier">POST /credentials</font> with the same email.'),
        bullet('Login step 1: <font face="Courier">POST /credentials/login</font> — store partial token in memory, render QR.'),
        bullet('Login step 2: <font face="Courier">POST /credentials/verify-2fa</font> — full JWT set as HttpOnly cookie automatically. '
               'Use <b>profileImageUrl</b> from the response body to display the avatar immediately.'),
        bullet('Dashboard: <font face="Courier">GET /accounts</font> for balances, <font face="Courier">GET /accounts/transactions</font> for activity feed.'),
        bullet('Account detail: <font face="Courier">GET /accounts/{accountId}</font>.'),
        bullet('Transactions: <font face="Courier">POST /accounts/delta</font>.'),
        bullet('Profile / avatar: <font face="Courier">GET /user/get/{userId}</font> for fresh URL + user data. '
               '<font face="Courier">PUT /user/update/{userId}</font> to update.'),
        bullet('Logout: <font face="Courier">POST /credentials/logout</font> with '
               '<font face="Courier">credentials: \'include\'</font> — server invalidates the active TOTP '
               'and clears X-Access-Token + X-Username cookies. Redirect to /login afterwards.'),
    ]
    story.append(Spacer(1, 0.08*inch))

    story.append(sub('Error handling cheatsheet'))
    story.append(tbl([
        ['Status', 'Meaning'],
        ['400',    'Plain-string error in body. Show it to the user.'],
        ['401',    'Token missing/expired or wrong type. Send user to /login.'],
        ['404',    'Empty body. Render not-found state.'],
        ['429',    'Rate limited. Read Retry-After header, disable the action.'],
        ['500',    'Server error. Offer retry, log to monitoring.'],
    ], cw_2col))

    story.append(sub('Things to keep in mind'))
    story += [
        bullet('No DELETE-user endpoint; only credentials can be deleted.'),
        bullet('Money is always { amount, currency }. Never send a bare number.'),
        bullet('Credit card fields are backend-computed. Display only, do not edit directly.'),
        bullet('Read endpoints eventually consistent (~500ms). Re-fetch after writes.'),
        bullet('TOTP regenerates every login — user re-pairs each session.'),
        bullet('<font face="Courier">userid_enc</font> is opaque — do not parse it.'),
        bullet('The full JWT is delivered as an <b>HttpOnly cookie (X-Access-Token)</b> — JavaScript cannot '
               'read it. The browser sends it automatically on all subsequent requests. '
               'Do <b>not</b> try to store or manage the full JWT in JavaScript.'),
        bullet('For cross-origin requests, add <font face="Courier">credentials: \'include\'</font> to your fetch/XHR options.'),
        bullet('<font face="Courier">profileImageUrl</font> expires in 1 min. Re-call GET /user/get/{userId} to refresh.'),
    ]
    story.append(PageBreak())

    # ─────────────────────────────────────────────
    # PAGE 17 — HTTP client wrapper
    # ─────────────────────────────────────────────
    story.append(sub('Suggested HTTP client wrapper'))
    story.append(code(
        '// pseudo-code, framework-agnostic\n'
        'const API_VERSION = "1.1";\n'
        '\n'
        'async function api(path, init = {}) {\n'
        '  const headers = new Headers(init.headers);\n'
        '  headers.set("Accept", `application/json;api-version=${API_VERSION}`);\n'
        '  // For the partial-auth step, set the Authorization header explicitly:\n'
        '  // headers.set("Authorization", `Bearer ${partialToken}`);\n'
        '  // After verify-2fa, the full JWT travels as the X-Access-Token cookie — no header needed.\n'
        '  if (init.body && !headers.has("Content-Type"))\n'
        '    headers.set("Content-Type", "application/json");\n'
        '\n'
        '  const res = await fetch(`${BASE_URL}${path}`, {\n'
        '    ...init,\n'
        '    headers,\n'
        '    credentials: "include",  // sends cookies cross-origin\n'
        '  });\n'
        '  if (res.status === 401) redirectToLogin();\n'
        '  if (res.status === 429) {\n'
        '    const retry = Number(res.headers.get("Retry-After") ?? 1);\n'
        '    throw new RateLimited(retry);\n'
        '  }\n'
        '  return res;\n'
        '}\n'
        '\n'
        '// Step 1: login — get partial token and QR code\n'
        'const { token: partialToken, qrCodeImage } =\n'
        '  await (await fetch(`${BASE_URL}/api/v1.1/credentials/login`, {\n'
        '    method:      "POST",\n'
        '    headers:     { "Content-Type": "application/json" },\n'
        '    body:        JSON.stringify({ login, plainPassword }),\n'
        '    credentials: "include",\n'
        '  })).json();\n'
        'renderQrCode(qrCodeImage);\n'
        '\n'
        '// Step 2: verify-2fa — full JWT set as cookie automatically\n'
        'const { profileImageUrl } =\n'
        '  await (await fetch(`${BASE_URL}/api/v1.1/credentials/verify-2fa`, {\n'
        '    method:      "POST",\n'
        '    headers:     {\n'
        '      "Content-Type":  "application/json",\n'
        '      "Authorization": `Bearer ${partialToken}`,\n'
        '    },\n'
        '    body:        JSON.stringify({ totpCode }),\n'
        '    credentials: "include",\n'
        '  })).json();\n'
        '// X-Access-Token cookie is now set — no token to store in JS\n'
        'if (profileImageUrl) showAvatar(profileImageUrl);\n'
        '\n'
        '// All subsequent requests — cookie sent automatically:\n'
        'const accounts = await (await api("/api/v1.1/accounts")).json();\n'
        'const txns = await (await api(\n'
        '  `/api/v1.1/accounts/transactions?from=${encodeURIComponent(from)}`\n'
        ')).json();\n'
        '\n'
        '// Refresh profile image URL after 1 minute\n'
        'const { user, profileImageUrl: freshUrl } =\n'
        '  await (await api(`/api/v1.1/user/get/${userId}`)).json();\n'
        'if (freshUrl) showAvatar(freshUrl);'
    ))

    doc.build(story, onFirstPage=footer, onLaterPages=footer)
    print(f'Generated: {out}')

build()
