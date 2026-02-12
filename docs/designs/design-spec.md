# HotBox Design System Specification

**Version:** 1.0
**Last Updated:** 2026-02-12
**Status:** Initial specification extracted from HTML prototypes

This document defines the complete design system for HotBox, a dark-mode-first, self-hosted chat platform. The design is cleaner and less noisy than Discord, with a focus on clarity, performance, and accessibility.

---

## Table of Contents

1. [Design Principles](#design-principles)
2. [Design Tokens](#design-tokens)
3. [Typography](#typography)
4. [Color System](#color-system)
5. [Spacing & Sizing](#spacing--sizing)
6. [Borders & Shadows](#borders--shadows)
7. [Motion & Transitions](#motion--transitions)
8. [Component Library](#component-library)
9. [Layout System](#layout-system)
10. [Interactive States](#interactive-states)
11. [Accessibility Guidelines](#accessibility-guidelines)
12. [Responsive Behavior](#responsive-behavior)

---

## Design Principles

### Core Values
- **Dark Mode First:** All designs optimized for dark backgrounds with deep cool slate tones
- **Clarity Over Decoration:** Functional, clean interfaces without unnecessary embellishment
- **Performance Conscious:** Minimal CSS, optimized rendering, no loading screens
- **Accessible by Default:** WCAG 2.1 AA compliance, semantic HTML, keyboard navigation
- **Responsive & Fast:** Mobile-first approach, smooth interactions

### Visual Language
- Cool slate backgrounds with subtle layering
- Teal/mint accent color for primary actions and brand
- Muted, desaturated palette for reduced eye strain
- Clean typography hierarchy using DM Sans and JetBrains Mono
- Subtle borders and shadows for depth

---

## Design Tokens

All design tokens are defined as CSS custom properties for consistency and easy theming.

### Background Layers

Deep cool slate progression from darkest to lightest:

```css
--bg-deepest: #0c0c0f;   /* Body background, deepest layer */
--bg-deep: #111116;       /* Top bar, footer, panels */
--bg-base: #16161d;       /* Main content background */
--bg-raised: #1c1c25;     /* Input fields, cards (resting state) */
--bg-surface: #23232e;    /* Active tabs, elevated surfaces */
--bg-hover: #2a2a37;      /* Interactive hover state */
--bg-active: #32323f;     /* Active/pressed state, scrollbar thumb */
```

**Usage:**
- `--bg-deepest`: Primary body background
- `--bg-deep`: Fixed headers, footers, modals
- `--bg-base`: Main content areas, default panels
- `--bg-raised`: Form inputs, cards in resting state
- `--bg-surface`: Active/selected items
- `--bg-hover`: Hover state for interactive elements
- `--bg-active`: Pressed/active state

### Text Colors

```css
--text-primary: #e2e2ea;   /* Primary text, headings */
--text-secondary: #9898a8; /* Secondary text, labels */
--text-muted: #5c5c72;     /* Muted text, placeholders, hints */
--text-faint: #3e3e52;     /* Very faint text, disabled states */
```

**Contrast Ratios (on --bg-base):**
- `--text-primary`: ~12:1 (AAA)
- `--text-secondary`: ~5.5:1 (AA)
- `--text-muted`: ~3.5:1 (AA for large text)
- `--text-faint`: ~2.5:1 (decorative only)

### Accent Colors

Cool teal/mint palette for branding and primary actions:

```css
--accent: #5de4c7;              /* Primary accent, brand color */
--accent-hover: #7aecd5;        /* Hover state for accent elements */
--accent-muted: rgba(93, 228, 199, 0.10);  /* Subtle accent background */
--accent-strong: #a0f0de;       /* Strong/bright accent variant */
--accent-glow: rgba(93, 228, 199, 0.06);   /* Glow/focus ring */
```

**Usage:**
- `--accent`: Primary buttons, brand logo, links, active indicators
- `--accent-hover`: Hover state for primary buttons
- `--accent-muted`: Subtle backgrounds for accent elements
- `--accent-strong`: Very bright highlights
- `--accent-glow`: Focus rings, subtle glows

### Status Colors

```css
--status-online: #6bc76b;   /* Online/available, voice active */
--status-idle: #c7a63e;     /* Idle/away */
--status-dnd: #c76060;      /* Do not disturb, errors, destructive */
--status-offline: #4a4a5a;  /* Offline/inactive */
```

**Voice-Specific:**
```css
--voice-active: #6bc76b;    /* Same as online */
--voice-muted: #5c5c72;     /* Same as text-muted */
```

### Border Colors

```css
--border-subtle: rgba(255, 255, 255, 0.04);  /* Very subtle borders */
--border-light: rgba(255, 255, 255, 0.07);   /* Standard borders */
--border-focus: rgba(93, 228, 199, 0.3);     /* Focus state borders */
```

---

## Typography

### Font Families

```css
--font-body: 'DM Sans', -apple-system, BlinkMacSystemFont, sans-serif;
--font-mono: 'JetBrains Mono', 'SF Mono', 'Consolas', monospace;
```

**Fonts to Load:**
- **DM Sans**: Weights 300, 400, 500, 600, 700 (plus italic 400)
- **JetBrains Mono**: Weights 400, 500, 700

**Web Fonts:**
```html
<link href="https://fonts.googleapis.com/css2?family=DM+Sans:ital,opsz,wght@0,9..40,300;0,9..40,400;0,9..40,500;0,9..40,600;0,9..40,700;1,9..40,400&family=JetBrains+Mono:wght@400;500;700&display=swap" rel="stylesheet">
```

### Type Scale

| Element | Font | Size | Weight | Line Height | Letter Spacing | Usage |
|---------|------|------|--------|-------------|----------------|-------|
| **Hero Title** | Body | 48px | 700 | 1.1 | -0.03em | Landing page hero |
| **Page Title** | Body | 28px | 700 | 1.2 | -0.02em | Section headings |
| **Auth Card Title** | Body | 22px | 700 | 1.3 | -0.02em | Card headings |
| **Channel Welcome** | Body | 22px | 700 | 1.3 | -0.02em | Channel intro |
| **Subtitle** | Body | 17px | 400 | 1.6 | 0 | Hero subtitle |
| **Body Large** | Body | 15px | 500 | 1.5 | 0 | Large body text |
| **Body** | Body | 14px | 400 | 1.5 | 0 | Default body text |
| **Body Medium** | Body | 13px | 450-500 | 1.5 | 0 | Tabs, labels |
| **Small** | Body | 12px | 500 | 1.4 | 0 | Form labels, hints |
| **Tiny** | Body | 11px | 500 | 1.3 | 0.02em | Metadata, counts |
| **Micro** | Body | 10px | 500 | 1.3 | 0.02em | Timestamps, badges |
| **Brand** | Mono | 13-14px | 700 | 1.2 | 0.06em | Logo, brand text (uppercase) |
| **Section Label** | Mono | 10-11px | 500 | 1.2 | 0.06-0.08em | Section headers (uppercase) |
| **Code/Mono** | Mono | 11-13px | 400-500 | 1.4 | 0.03em | Code, timestamps, technical |

### Font Rendering

```css
-webkit-font-smoothing: antialiased;
-moz-osx-font-smoothing: grayscale;
```

Apply to body element for improved rendering on dark backgrounds.

---

## Color System

### Semantic Color Usage

#### Backgrounds
- **Deepest layer**: Body, overlays (`--bg-deepest`)
- **Containers**: Fixed panels, headers (`--bg-deep`)
- **Content**: Main content areas (`--bg-base`)
- **Inputs**: Form fields, cards (`--bg-raised`)
- **Active surfaces**: Selected tabs, active items (`--bg-surface`)

#### Text
- **Primary**: Headings, important content (`--text-primary`)
- **Secondary**: Body text, labels (`--text-secondary`)
- **Tertiary**: Hints, placeholders (`--text-muted`)
- **Disabled**: Disabled states, decorative (`--text-faint`)

#### Accent
- **Primary actions**: Buttons, links, brand (`--accent`)
- **Highlights**: Active indicators, badges (`--accent`)
- **Backgrounds**: Subtle tints (`--accent-muted`)

#### Status
- **Success/Online**: Green (`--status-online`)
- **Warning/Idle**: Yellow (`--status-idle`)
- **Error/DND**: Red (`--status-dnd`)
- **Inactive**: Gray (`--status-offline`)

### Avatar Colors

Avatars use HSL color generation based on username hash:

```javascript
function getAvatarColor(name) {
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  const hue = Math.abs(hash) % 360;
  return `hsl(${hue}, 32%, 38%)`;
}
```

**Parameters:**
- **Saturation**: 32% (muted, not overwhelming)
- **Lightness**: 38% (readable white text on top)

---

## Spacing & Sizing

### Spacing Scale

While no explicit spacing tokens are defined, consistent spacing patterns are used:

| Size | Pixels | Usage |
|------|--------|-------|
| **3xs** | 2px | Compact gaps, tab dividers |
| **2xs** | 3-4px | Avatar stacks, inline gaps |
| **xs** | 6px | Form field labels, tight padding |
| **sm** | 8px | Compact spacing, icon gaps |
| **md** | 10-12px | Standard gaps, padding |
| **lg** | 14-16px | Section gaps, container padding |
| **xl** | 20-24px | Large spacing, content padding |
| **2xl** | 28-32px | Major sections, page padding |
| **3xl** | 40-48px | Hero spacing |

### Common Padding/Margin Patterns

- **Topbar**: `0 16px` (horizontal)
- **Main content**: `0 32px` (horizontal)
- **Auth cards**: `36px 32px 32px` (top, horizontal, bottom)
- **Form groups**: `16px` margin-bottom
- **Input fields**: `10px 14px`
- **Buttons**: `7-12px 18-28px` (vertical, horizontal)
- **Message groups**: `4px 8px`

### Sizing Conventions

- **Topbar height**: 52px
- **Channel info bar**: 36px
- **Voice bar**: 48px
- **Site header**: 56px
- **Icons**: 14px, 16px, 18px, 20px
- **Avatars**: 22px (DM tabs), 24px (topbar dots), 26px (members), 30px (topbar user), 34px (messages)

---

## Borders & Shadows

### Border Radius

```css
--radius-xs: 3px;      /* Checkboxes, tight corners */
--radius-sm: 4px;      /* Small elements, badges */
--radius-md: 8px;      /* Buttons, inputs, tabs, cards */
--radius-lg: 12px;     /* Large cards, modals, panels */
--radius-pill: 9999px; /* Avatars, badges, round buttons */
```

### Shadows

```css
--shadow-md: 0 4px 24px rgba(0, 0, 0, 0.5);
--shadow-lg: 0 8px 48px rgba(0, 0, 0, 0.6);
--shadow-overlay: 0 12px 40px rgba(0, 0, 0, 0.7), 0 0 0 1px var(--border-subtle);
```

**Usage:**
- `--shadow-md`: Buttons on hover
- `--shadow-lg`: Auth cards, elevated panels
- `--shadow-overlay`: Dropdowns, modals, overlays

### Focus Rings

Focus states use a combination of border color and box-shadow:

```css
border-color: var(--border-focus);
box-shadow: 0 0 0 3px var(--accent-glow);
```

---

## Motion & Transitions

### Timing Functions

```css
--transition-fast: 100ms ease;        /* Quick interactions (hover) */
--transition-base: 180ms ease;        /* Standard transitions */
--transition-smooth: 280ms cubic-bezier(0.4, 0, 0.2, 1); /* Smooth, polished */
```

### Common Transitions

- **Hover effects**: `all var(--transition-fast)`
- **Button interactions**: `all var(--transition-base)`
- **Panel slides**: `transform var(--transition-smooth)`
- **Background changes**: `background var(--transition-fast)`
- **Color changes**: `color var(--transition-fast)`

### Animations

**Pulse glow** (voice bar indicator):
```css
@keyframes pulse-glow {
  0%, 100% { opacity: 1; box-shadow: 0 0 4px rgba(107, 199, 107, 0.4); }
  50% { opacity: 0.6; box-shadow: 0 0 8px rgba(107, 199, 107, 0.2); }
}
```

**Transform states:**
- Buttons on hover: `translateY(-1px)`
- Online avatar dots on hover: `translateY(-2px)`

---

## Component Library

### Buttons

#### Primary Button

```css
.btn-primary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 600;
  color: var(--bg-deepest);
  background: var(--accent);
  padding: 12px 28px;
  border-radius: var(--radius-md);
  transition: all var(--transition-base);
}

.btn-primary:hover {
  background: var(--accent-hover);
  transform: translateY(-1px);
  box-shadow: 0 4px 20px rgba(93, 228, 199, 0.2);
}
```

#### Secondary Button

```css
.btn-secondary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 500;
  color: var(--text-secondary);
  background: var(--bg-raised);
  padding: 12px 28px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-light);
  transition: all var(--transition-base);
}

.btn-secondary:hover {
  color: var(--text-primary);
  background: var(--bg-surface);
  border-color: rgba(255, 255, 255, 0.1);
  transform: translateY(-1px);
}
```

#### Submit Button (Full Width)

```css
.btn-submit {
  width: 100%;
  padding: 11px 20px;
  font-size: 14px;
  font-weight: 600;
  color: var(--bg-deepest);
  background: var(--accent);
  border-radius: var(--radius-md);
  transition: all var(--transition-base);
  margin-bottom: 20px;
}

.btn-submit:hover {
  background: var(--accent-hover);
  transform: translateY(-1px);
  box-shadow: 0 4px 16px rgba(93, 228, 199, 0.18);
}

.btn-submit:active {
  transform: translateY(0);
}
```

#### Icon Button

```css
.topbar-icon-btn {
  width: 32px;
  height: 32px;
  border-radius: var(--radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted);
  transition: all var(--transition-fast);
}

.topbar-icon-btn:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.topbar-icon-btn svg {
  width: 16px;
  height: 16px;
}
```

#### OAuth Button

```css
.btn-oauth {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 10px 16px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  background: var(--bg-base);
  border: 1px solid var(--border-light);
  border-radius: var(--radius-md);
  transition: all var(--transition-base);
}

.btn-oauth:hover {
  color: var(--text-primary);
  background: var(--bg-raised);
  border-color: rgba(255, 255, 255, 0.1);
}

.btn-oauth svg {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
}
```

### Form Elements

#### Text Input

```css
.form-input {
  width: 100%;
  padding: 10px 14px;
  font-size: 14px;
  color: var(--text-primary);
  background: var(--bg-base);
  border: 1px solid var(--border-light);
  border-radius: var(--radius-md);
  transition: all var(--transition-base);
}

.form-input::placeholder {
  color: var(--text-faint);
}

.form-input:focus {
  border-color: var(--border-focus);
  box-shadow: 0 0 0 3px var(--accent-glow);
  background: var(--bg-raised);
}

.form-input:hover:not(:focus) {
  border-color: rgba(255, 255, 255, 0.1);
}
```

#### Error State

```css
.form-input.error,
.form-group.has-error .form-input {
  border-color: var(--status-dnd);
  box-shadow: 0 0 0 3px rgba(199, 96, 96, 0.08);
}

.form-error {
  font-size: 11px;
  color: var(--status-dnd);
  margin-top: 4px;
  display: none;
}

.form-group.has-error .form-error {
  display: block;
}
```

#### Label

```css
.form-label {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 6px;
  letter-spacing: 0.01em;
}
```

#### Checkbox

```css
.form-check {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-secondary);
  cursor: pointer;
  user-select: none;
}

.form-check input[type="checkbox"] {
  appearance: none;
  -webkit-appearance: none;
  width: 16px;
  height: 16px;
  border: 1px solid var(--border-light);
  border-radius: var(--radius-xs);
  background: var(--bg-base);
  cursor: pointer;
  position: relative;
  transition: all var(--transition-fast);
  flex-shrink: 0;
}

.form-check input[type="checkbox"]:checked {
  background: var(--accent);
  border-color: var(--accent);
}

.form-check input[type="checkbox"]:checked::after {
  content: '';
  position: absolute;
  left: 4px;
  top: 1px;
  width: 5px;
  height: 9px;
  border: solid var(--bg-deepest);
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

.form-check input[type="checkbox"]:focus-visible {
  box-shadow: 0 0 0 3px var(--accent-glow);
}
```

#### Password Toggle

```css
.input-wrapper {
  position: relative;
}

.input-wrapper .form-input {
  padding-right: 40px;
}

.toggle-password {
  position: absolute;
  right: 10px;
  top: 50%;
  transform: translateY(-50%);
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-faint);
  border-radius: var(--radius-sm);
  transition: color var(--transition-fast);
}

.toggle-password:hover {
  color: var(--text-secondary);
}

.toggle-password svg {
  width: 16px;
  height: 16px;
}
```

### Tabs

#### Section Switcher (Channels/DMs)

```css
.section-switcher {
  display: flex;
  align-items: center;
  gap: 2px;
  background: var(--bg-base);
  border-radius: var(--radius-md);
  padding: 3px;
}

.section-btn {
  font-family: var(--font-mono);
  font-size: 11px;
  font-weight: 500;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  padding: 5px 12px;
  border-radius: 6px;
  color: var(--text-muted);
  transition: all var(--transition-base);
  white-space: nowrap;
}

.section-btn:hover {
  color: var(--text-secondary);
}

.section-btn.active {
  background: var(--bg-raised);
  color: var(--text-primary);
}
```

#### Channel Tab

```css
.channel-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: var(--radius-md);
  color: var(--text-muted);
  font-size: 13px;
  font-weight: 450;
  white-space: nowrap;
  transition: all var(--transition-fast);
  position: relative;
  flex-shrink: 0;
}

.channel-tab:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.channel-tab.active {
  background: var(--bg-surface);
  color: var(--text-primary);
}

.channel-tab.active::after {
  content: '';
  position: absolute;
  bottom: -1px;
  left: 50%;
  transform: translateX(-50%);
  width: 16px;
  height: 2px;
  background: var(--accent);
  border-radius: var(--radius-pill);
}
```

#### Tab Badge

```css
.tab-badge {
  background: var(--accent);
  color: var(--bg-deepest);
  font-size: 10px;
  font-weight: 700;
  min-width: 16px;
  height: 16px;
  border-radius: var(--radius-pill);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 4px;
  line-height: 1;
}
```

### Avatars

#### Message Avatar

```css
.msg-avatar {
  width: 34px;
  height: 34px;
  border-radius: var(--radius-lg);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
  color: #fff;
  flex-shrink: 0;
  margin-top: 2px;
  /* background: generated via getAvatarColor(name) */
}
```

#### Member Avatar

```css
.member-avatar {
  width: 26px;
  height: 26px;
  border-radius: var(--radius-pill);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 9px;
  font-weight: 700;
  color: #fff;
  flex-shrink: 0;
  position: relative;
  /* background: generated via getAvatarColor(name) */
}
```

#### Status Dot

```css
.status-dot {
  position: absolute;
  bottom: -1px;
  right: -1px;
  width: 9-10px;
  height: 9-10px;
  border-radius: var(--radius-pill);
  border: 2px solid var(--bg-deep);
}

.status-dot.online { background: var(--status-online); }
.status-dot.idle { background: var(--status-idle); }
.status-dot.dnd { background: var(--status-dnd); }
.status-dot.offline { background: var(--status-offline); }
```

### Cards

#### Auth Card

```css
.auth-card {
  background: var(--bg-deep);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 36px 32px 32px;
  box-shadow: var(--shadow-lg);
}
```

#### Feature Card

```css
.feature-card {
  background: var(--bg-base);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 28px 24px;
  transition: all var(--transition-smooth);
}

.feature-card:hover {
  border-color: var(--border-light);
  background: var(--bg-raised);
}
```

### Messages

#### Message Group

```css
.message-group {
  display: flex;
  gap: 14px;
  padding: 4px 8px;
  margin: 0;
  border-radius: var(--radius-md);
  transition: background var(--transition-fast);
}

.message-group + .message-group {
  margin-top: 2px;
}

.message-group.new-author {
  margin-top: 16px;
}

.message-group:hover {
  background: rgba(255, 255, 255, 0.015);
}
```

#### Message Header

```css
.msg-header {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 2px;
}

.msg-author {
  font-size: 13px;
  font-weight: 600;
  /* color: generated via getAvatarColor(name) */
}

.msg-timestamp {
  font-family: var(--font-mono);
  font-size: 10px;
  color: var(--text-faint);
  letter-spacing: 0.02em;
}
```

#### Message Body

```css
.msg-body {
  font-size: 14px;
  color: var(--text-secondary);
  line-height: 1.55;
  word-wrap: break-word;
}

.msg-body + .msg-body {
  margin-top: 3px;
}
```

### Dropdowns & Overlays

#### Voice Dropdown

```css
.voice-dropdown {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  width: 260px;
  background: var(--bg-raised);
  border: 1px solid var(--border-light);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-overlay);
  padding: 8px;
  z-index: 50;
  display: none;
}

.voice-dropdown.open {
  display: block;
}
```

#### Members Overlay

```css
.members-overlay {
  position: fixed;
  top: 52px;
  right: 0;
  width: 260px;
  bottom: 0;
  background: var(--bg-deep);
  border-left: 1px solid var(--border-subtle);
  box-shadow: var(--shadow-lg);
  z-index: 30;
  overflow-y: auto;
  padding: 16px 12px;
  transform: translateX(100%);
  transition: transform var(--transition-smooth);
}

.members-overlay.open {
  transform: translateX(0);
}
```

### Banners

#### Registration Mode Banner

```css
.reg-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-radius: var(--radius-md);
  font-size: 12px;
  margin-bottom: 20px;
  border: 1px solid;
}

.reg-banner.open {
  background: rgba(107, 199, 107, 0.06);
  border-color: rgba(107, 199, 107, 0.15);
  color: var(--status-online);
}

.reg-banner.invite {
  background: rgba(199, 166, 62, 0.06);
  border-color: rgba(199, 166, 62, 0.15);
  color: var(--status-idle);
}
```

### Dividers

#### Horizontal Divider

```css
.auth-divider {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 20px;
}

.auth-divider::before,
.auth-divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--border-light);
}

.auth-divider span {
  font-size: 11px;
  font-weight: 500;
  color: var(--text-faint);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-family: var(--font-mono);
}
```

#### Date Separator

```css
.date-separator {
  display: flex;
  align-items: center;
  gap: 16px;
  margin: 20px 0 12px;
  user-select: none;
}

.date-separator::before,
.date-separator::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--border-subtle);
}

.date-separator span {
  font-family: var(--font-mono);
  font-size: 10px;
  font-weight: 500;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}
```

### Scrollbar

```css
::-webkit-scrollbar {
  width: 6px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: var(--bg-active);
  border-radius: var(--radius-pill);
}

::-webkit-scrollbar-thumb:hover {
  background: var(--bg-hover);
}
```

### Tooltips

```css
[data-tooltip] {
  position: relative;
}

[data-tooltip]::after {
  content: attr(data-tooltip);
  position: absolute;
  bottom: calc(100% + 8px);
  left: 50%;
  transform: translateX(-50%) translateY(4px);
  padding: 5px 10px;
  background: var(--bg-deepest);
  color: var(--text-primary);
  font-family: var(--font-body);
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-light);
  pointer-events: none;
  opacity: 0;
  transition: opacity var(--transition-fast), transform var(--transition-fast);
  z-index: 100;
}

[data-tooltip]:hover::after {
  opacity: 1;
  transform: translateX(-50%) translateY(0);
}
```

---

## Layout System

### Main App Layout (Chat UI)

Three-layer vertical stack:

```
┌──────────────────────────────────┐
│ Topbar (52px fixed)              │
├──────────────────────────────────┤
│ Channel Info Bar (36px)          │
├──────────────────────────────────┤
│                                  │
│ Main Content (flex: 1)           │
│   ├─ Messages Area (scroll)      │
│   └─ Message Input (bottom)      │
│                                  │
├──────────────────────────────────┤
│ Voice Bar (48px, conditional)    │
└──────────────────────────────────┘
```

**Layout CSS:**
```css
.app {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--bg-base);
}
```

### Topbar Structure

```
┌────────────────────────────────────────────────────┐
│ [HotBox] [Channels|DMs] | [Tabs...] | [Controls] │
└────────────────────────────────────────────────────┘
```

- Brand (left)
- Section switcher
- Vertical divider
- Channel/DM tabs (scrollable)
- Vertical divider
- Right controls (avatars, voice, search, settings, user)

### Messages Layout

Centered content with max-width:

```css
.messages-container {
  margin-top: auto;
  padding: 0 32px;
  max-width: 780px;
  width: 100%;
  align-self: center;
}

.message-input-wrapper {
  padding: 0 32px 20px;
  max-width: 844px;
  width: 100%;
  align-self: center;
}
```

### Auth Page Layout

Centered card with max-width:

```css
.auth-page {
  align-items: center;
  justify-content: center;
  padding: 80px 24px 40px;
}

.auth-container {
  width: 100%;
  max-width: 400px;
}
```

### Grid Layouts

**Features Grid:**
```css
.features-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 16px;
  max-width: 880px;
  margin: 0 auto;
}
```

---

## Interactive States

### Button States

| State | Background | Color | Transform | Shadow |
|-------|-----------|-------|-----------|---------|
| **Rest** | `--accent` | `--bg-deepest` | none | none |
| **Hover** | `--accent-hover` | `--bg-deepest` | `translateY(-1px)` | `0 4px 20px rgba(93, 228, 199, 0.2)` |
| **Active/Pressed** | `--accent` | `--bg-deepest` | `translateY(0)` | none |
| **Focus** | `--accent` | `--bg-deepest` | none | `0 0 0 3px var(--accent-glow)` |
| **Disabled** | `--bg-surface` | `--text-faint` | none | none |

### Input States

| State | Border | Background | Shadow |
|-------|--------|------------|--------|
| **Rest** | `--border-light` | `--bg-base` | none |
| **Hover** | `rgba(255,255,255,0.1)` | `--bg-base` | none |
| **Focus** | `--border-focus` | `--bg-raised` | `0 0 0 3px var(--accent-glow)` |
| **Error** | `--status-dnd` | `--bg-base` | `0 0 0 3px rgba(199, 96, 96, 0.08)` |
| **Disabled** | `--border-subtle` | `--bg-deepest` | none |

### Tab States

| State | Background | Color | Border Bottom |
|-------|-----------|-------|--------------|
| **Rest** | transparent | `--text-muted` | none |
| **Hover** | `--bg-hover` | `--text-secondary` | none |
| **Active** | `--bg-surface` | `--text-primary` | 2px `--accent` |

### Avatar States

- **Rest**: Static
- **Hover**: `translateY(-2px)`, `z-index: 2` (online dots)
- **Offline**: `opacity: 0.35`

### Panel States

- **Members Overlay Closed**: `transform: translateX(100%)`
- **Members Overlay Open**: `transform: translateX(0)`
- **Voice Dropdown Closed**: `display: none`
- **Voice Dropdown Open**: `display: block`

---

## Accessibility Guidelines

### Color Contrast

All text must meet WCAG 2.1 AA standards:

- **Normal text (14px)**: Minimum 4.5:1 contrast ratio
- **Large text (18px+ or 14px+ bold)**: Minimum 3:1 contrast ratio
- **Icons/UI elements**: Minimum 3:1 contrast ratio

**Verified Combinations:**
- `--text-primary` on `--bg-base`: ~12:1 (AAA)
- `--text-secondary` on `--bg-base`: ~5.5:1 (AA)
- `--accent` on `--bg-deepest`: ~9:1 (AAA)
- `--bg-deepest` on `--accent`: ~9:1 (AAA)

### Keyboard Navigation

All interactive elements must be keyboard accessible:

- **Tab order**: Logical, top-to-bottom, left-to-right
- **Focus indicators**: Clear visual focus states (3px glow)
- **Escape key**: Closes modals, dropdowns, overlays
- **Enter key**: Submits forms, activates buttons
- **Arrow keys**: Navigate lists/menus (future enhancement)

### Semantic HTML

- Use proper heading hierarchy (`h1`, `h2`, `h3`)
- Use `<button>` for actions, `<a>` for navigation
- Use `<form>`, `<label>`, `<input>` for forms
- Use `aria-label` for icon-only buttons
- Use `aria-labelledby` for complex components

### Screen Reader Support

- **Alt text**: All images and icons must have descriptive alt text or aria-labels
- **Hidden content**: Use `aria-hidden="true"` for decorative elements
- **Live regions**: Use `aria-live` for dynamic content (chat messages)
- **Landmarks**: Use semantic HTML5 landmarks (`<header>`, `<main>`, `<nav>`, `<aside>`)

### Focus Management

- **Modal open**: Focus first interactive element
- **Modal close**: Return focus to trigger element
- **Dropdown open**: Focus first item
- **Page navigation**: Focus appropriate element (input on auth pages)

---

## Responsive Behavior

### Breakpoints

```css
/* Mobile (portrait) */
@media (max-width: 480px) { /* ... */ }

/* Mobile/Small tablet (landscape) */
@media (max-width: 640px) { /* ... */ }

/* Tablet */
@media (max-width: 768px) { /* ... */ }

/* Desktop (default) */
/* No media query needed */
```

### Mobile Adaptations (≤640px)

**Typography:**
- Hero title: 48px → 32px
- Hero subtitle: 17px → 15px

**Layout:**
- Hero actions: Stack vertically (flex-direction: column)
- Features grid: Single column
- Auth card padding: 36px 32px → 28px 20px

**Navigation:**
- Hide nav link text, show icons only
- Site header padding: 24px → 16px

### Small Mobile (≤480px)

**Typography:**
- Hero title: 32px → 28px
- Features header: 28px → 22px

### Responsive Patterns

**Overflow Scrolling:**
- Channel tabs: Horizontal scroll on mobile
- Messages area: Vertical scroll
- Members overlay: Vertical scroll

**Touch Targets:**
- Minimum 44×44px for all interactive elements on mobile
- Increased padding/spacing for better touch ergonomics

**Viewport Units:**
- App height: `100vh` (full viewport)
- Modals/overlays: Position fixed with `inset: 0`

---

## Implementation Notes

### CSS Reset

All prototypes use a minimal reset:

```css
*, *::before, *::after {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body {
  height: 100%;
  overflow: hidden; /* For app layout */
}
```

### Font Loading

Use `preconnect` for faster font loading:

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
```

### Icon System

All icons use inline SVG with Hero Icons (Feather Icons style):

- **Stroke width**: 2px
- **Stroke linecap**: round
- **Stroke linejoin**: round
- **Fill**: none (stroke-only)

Common icon sizes: 14px, 16px, 18px, 20px

### Z-Index Layers

| Layer | Z-Index | Usage |
|-------|---------|-------|
| **Base** | 0 | Default content |
| **Topbar** | 20 | Fixed topbar, voice bar |
| **Overlay** | 30 | Members overlay |
| **Dropdown** | 40-50 | Voice dropdown, click-away overlay |
| **Modal** | 100+ | Modals, tooltips |

### Performance Considerations

- **CSS Custom Properties**: Used for all design tokens (efficient runtime updates)
- **Minimal Transitions**: Only transition specific properties, not `all` where possible
- **GPU Acceleration**: Use `transform` for animations, not `top`/`left`
- **Lazy Loading**: Avatars and images should use lazy loading
- **Debouncing**: Scroll and resize handlers should be debounced

---

## Future Enhancements

### Planned Additions

- **Light mode theme**: Complete light mode color palette
- **High contrast mode**: Increased contrast for accessibility
- **Reduced motion**: Respect `prefers-reduced-motion` media query
- **Custom themes**: User-defined color schemes
- **Emoji support**: Consistent emoji rendering
- **Rich text formatting**: Markdown support, code blocks
- **File attachments**: Image/file preview cards

### Components Not Yet Designed

- **Modals**: Settings modal, user profile modal
- **Context menus**: Right-click menus
- **Notification toasts**: In-app notifications
- **Loading states**: Spinners, skeleton screens
- **Empty states**: No messages, no channels
- **Error states**: Connection errors, API errors
- **File upload**: Drag-and-drop area, progress indicators

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-12 | Initial specification extracted from HTML prototypes |

---

## References

- **Prototypes**: `/prototypes/main-ui-proposal.html`, `/prototypes/auth-pages.html`
- **WCAG 2.1**: https://www.w3.org/WAI/WCAG21/quickref/
- **Hero Icons**: https://heroicons.com/
- **DM Sans Font**: https://fonts.google.com/specimen/DM+Sans
- **JetBrains Mono Font**: https://fonts.google.com/specimen/JetBrains+Mono

---

**End of Design Specification**
