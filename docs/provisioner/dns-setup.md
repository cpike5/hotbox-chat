# DNS Setup Guide for HotBox Provisioner

This guide walks you through setting up DNS and wildcard SSL certificates for the HotBox Provisioner using Namecheap as your DNS provider.

When you run the provisioner, each chat instance gets its own subdomain like `thbr.hotboxchat.ca`. The provisioner needs wildcard DNS (`*.hotboxchat.ca`) and wildcard SSL certificates to handle all these subdomains automatically. This guide shows you how to set that up.

## How It Works

Here's the traffic flow:

```
User Browser
    ↓
DNS Query: "What IP is instance123.hotboxchat.ca?"
    ↓
Your DNS Provider (Namecheap)
    ↓
Server IP: 192.0.2.1
    ↓
Your VPS running Caddy (reverse proxy)
    ↓
Caddy checks the subdomain, finds the right Docker container
    ↓
HotBox instance loads in the browser
```

The key pieces:
- **Wildcard DNS A record** (`*.hotboxchat.ca` → your server IP) — all subdomains point to your server
- **Wildcard SSL certificate** (covers `*.hotboxchat.ca`) — HTTPS works for all subdomains
- **Caddy reverse proxy** — routes traffic to the correct Docker container based on the subdomain

## Prerequisites

Before you start, you'll need:

- A registered domain (e.g., `hotboxchat.ca`) — you can register one at Namecheap, GoDaddy, etc.
- Your domain should be using **Namecheap's nameservers** (the default when you register a domain with them)
- A VPS or dedicated server with a static public IP address (DigitalOcean, Hetzner, Linode, etc.)
- SSH access to your server
- A Namecheap account with API access enabled
- Administrator access to your Namecheap account

## Step 1: Find Your Server's IP Address

First, you need your VPS's public IP. Most hosting dashboards show it prominently. You can also find it from your server itself:

```bash
curl ifconfig.me
```

This will output your public IP, something like `192.0.2.1`. **Write this down — you'll need it in the next step.**

> **Note:** Make sure this is your *public* IP, not a private/internal IP. If you're not sure, check your hosting provider's dashboard.

## Step 2: Configure DNS Records in Namecheap

You need to add two A records in Namecheap: one for your main domain and one for all subdomains.

### Log In to Namecheap

1. Go to [namecheap.com](https://www.namecheap.com/) and sign in to your account
2. Click **Domain List** in the left sidebar
3. Find your domain (e.g., `hotboxchat.ca`) and click **Manage**

### Add the DNS Records

1. Click the **Advanced DNS** tab
2. You should see a table with existing records. Look for records labeled "Parked Domain" or "Namecheap Parking Page" — **delete these** (you won't be using Namecheap's parking page)

3. In the same table, click **Add New Record**

**First record — for your main domain:**
- **Type:** A Record
- **Host:** @ (this means the root domain `hotboxchat.ca`)
- **Value:** Your server's IP (e.g., `192.0.2.1`)
- **TTL:** Automatic (or 5 min if you're testing)

Click the green checkmark to save.

**Second record — for all subdomains:**
- **Type:** A Record
- **Host:** * (the asterisk means "all subdomains")
- **Value:** Your server's IP (the same one)
- **TTL:** Automatic (or 5 min if you're testing)

Click the green checkmark to save.

When you're done, your Advanced DNS tab should look like this (you may have other records too):

```
Type  | Host | Value      | TTL
------|------|------------|----------
A     | @    | 192.0.2.1  | Automatic
A     | *    | 192.0.2.1  | Automatic
```

> **Note:** DNS changes can take a few minutes to propagate, or up to 24 hours in rare cases. Namecheap usually propagates in under 5 minutes.

## Step 3: Enable Namecheap API Access

Caddy needs API access to Namecheap's DNS to automatically prove it owns your domain and get wildcard SSL certificates. This requires enabling the Namecheap API.

### Check Your Account Eligibility

Before proceeding, note that **Namecheap requires your account to meet certain criteria to enable API access:**
- You must have at least 2 active domains registered with Namecheap, OR
- You must have a positive account balance (at least $1)

If you don't meet these requirements, see the section at the end of this guide for using Cloudflare DNS instead.

### Enable API Access

1. In Namecheap, click your **Account** menu (top right)
2. Click **Tools**
3. Click **API Access**
4. Toggle **API Access** to the ON position
5. Under **API Key**, you should see a long string — **copy it and save it somewhere safe** (you'll need it later)
6. Under **Whitelisted IPs**, add your server's public IP address. Click **Add IP**, paste your IP, and click the checkmark
7. Click **Save Changes** at the bottom of the page

> **Important:** Your API key is sensitive — treat it like a password. Don't commit it to version control or share it.

You now have:
- Namecheap API key (a long string of letters and numbers)
- Your server IP whitelisted on the Namecheap API

You're ready to configure Caddy.

## Step 4: Configure Caddy for Wildcard Certificates

Caddy is a reverse proxy that automatically manages SSL certificates. By default, Caddy doesn't support the Namecheap API — you need to build a custom Caddy binary with the Namecheap DNS plugin.

### Option A: Use Docker with Pre-built Caddy Image (Recommended)

The provisioner likely uses Docker. The easiest way is to build a custom Docker image with the Namecheap plugin:

**1. Create a Dockerfile for Caddy with Namecheap support:**

Save this as `Dockerfile.caddy` in your provisioner directory:

```dockerfile
FROM caddy:latest

RUN caddy build \
    --with github.com/caddy-dns/namecheap
```

**2. Build the image:**

```bash
docker build -f Dockerfile.caddy -t caddy-namecheap:latest .
```

**3. In your `docker-compose.yml`, use this image instead of the default caddy:**

```yaml
services:
  caddy:
    image: caddy-namecheap:latest  # Your custom image
    container_name: provisioner-caddy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    environment:
      - NAMECHEAP_API_KEY=${NAMECHEAP_API_KEY}
      - NAMECHEAP_API_USER=${NAMECHEAP_API_USER}
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - provisioner
```

### Option B: Use a Pre-built Caddy Binary (if not using Docker)

If you're running on bare metal, download a pre-built Caddy binary with the Namecheap plugin:

1. Go to https://caddyserver.com/download
2. Select your platform (Linux AMD64, etc.)
3. Under "Additional Plugins," search for and select `namecheap` from the DNS providers
4. Click **Download** — this gives you a custom caddy binary
5. Extract it and place it in your PATH (or your provisioner directory)

```bash
# Example
tar xzf caddy_linux_amd64.tar.gz
sudo mv caddy /usr/local/bin/
```

### Create the Caddyfile

Create a `Caddyfile` in your provisioner directory. This tells Caddy how to handle your domain:

```
# Wildcard certificate for all instances
*.hotboxchat.ca {
    tls {
        dns namecheap {
            api_key {env.NAMECHEAP_API_KEY}
            user {env.NAMECHEAP_API_USER}
        }
    }

    # Reverse proxy rules are added dynamically by the provisioner
    # The provisioner watches for new instances and adds routes here
    reverse_proxy localhost:8000
}

# Base domain — points to the provisioner dashboard
hotboxchat.ca {
    reverse_proxy localhost:8000
}
```

### Set Environment Variables

Caddy needs your Namecheap credentials as environment variables. Create or update your `.env` file:

```bash
NAMECHEAP_API_KEY=<your-api-key-from-step-3>
NAMECHEAP_API_USER=<your-namecheap-username>
```

> **Important:** The `NAMECHEAP_API_USER` is your Namecheap username (the one you use to log in), not your email address.

### Restart Caddy

If using Docker Compose:

```bash
docker compose up -d caddy
```

Watch the logs to confirm the certificate is being issued:

```bash
docker compose logs -f caddy
```

You should see something like:

```
caddy_1 | {"level":"info","ts":1234567890,"msg":"provisioning certificate","domain":"hotboxchat.ca"}
caddy_1 | {"level":"info","ts":1234567891,"msg":"provisioning certificate","domain":"*.hotboxchat.ca"}
...
caddy_1 | {"level":"info","ts":1234567900,"msg":"certificate obtained successfully"}
```

If you see errors, jump to the **Troubleshooting** section below.

## Step 5: Verify It Works

### Check DNS Propagation

From your local machine, check that DNS is working:

```bash
dig *.hotboxchat.ca
# or
nslookup hotboxchat.ca
```

You should see your server's IP in the response. If not, DNS hasn't propagated yet — wait a few minutes and try again.

You can also use an online tool like [dnschecker.org](https://dnschecker.org) — just enter `hotboxchat.ca` and check the A record.

### Check Caddy Logs for Certificate Success

From your server:

```bash
docker compose logs caddy | grep -i "certificate\|error"
```

Look for a message like `"certificate obtained successfully"`. If you see error messages, see the **Troubleshooting** section.

### Test with HTTPS

Visit your domain in a browser:

```
https://hotboxchat.ca
```

Your browser should show a valid SSL certificate (no warnings). If you see a certificate error or the page doesn't load, check the **Troubleshooting** section.

### Create a Test Instance

Use your provisioner's API or dashboard to create a test instance. It should generate a subdomain like:

```
https://instance-name.hotboxchat.ca
```

Visit it in your browser. The certificate should be valid, and the page should load.

## Troubleshooting

### DNS Not Propagating

**Problem:** `dig` shows "server can't find" or returns wrong IP

**Solutions:**
- Wait a few minutes — DNS can take up to 5 minutes to propagate globally
- Check you entered the IP correctly in Namecheap (Step 2)
- Flush your local DNS cache:
  ```bash
  # macOS
  sudo dscacheutil -flushcache

  # Linux
  sudo systemctl restart systemd-resolved

  # Windows
  ipconfig /flushdns
  ```
- Use a public DNS resolver to double-check:
  ```bash
  dig @8.8.8.8 hotboxchat.ca  # Google DNS
  ```

### Caddy Can't Get Wildcard Certificate

**Problem:** Caddy logs show DNS validation failed

**Possible causes and solutions:**

1. **Namecheap API key is wrong or not set**
   - Double-check your `.env` file has `NAMECHEAP_API_KEY` and `NAMECHEAP_API_USER`
   - Make sure the key is the exact string from Namecheap (no extra spaces)
   - Restart Caddy: `docker compose restart caddy`

2. **Server IP not whitelisted**
   - Go back to Namecheap → Account → Tools → API Access
   - Verify your server IP is in the "Whitelisted IPs" list
   - If it's wrong, delete it and add the correct IP
   - Restart Caddy

3. **Namecheap API access not enabled on your account**
   - Go to Namecheap → Account → Tools → API Access
   - Check that the toggle is ON (blue)
   - If it's OFF or grayed out, your account may not meet the requirements (see Step 3)

4. **DNS records not created yet**
   - Verify both A records (`@` and `*`) are in Namecheap's Advanced DNS tab
   - Wait for DNS to fully propagate (5-10 minutes)

**Check the logs:**

```bash
docker compose logs caddy | tail -50
```

Look for error messages mentioning "dns", "namecheap", or "validation".

### Subdomain Resolves But Connection Refused

**Problem:** `ping instance.hotboxchat.ca` works but visiting in browser gives "Connection refused"

**Possible causes:**

1. **Caddy is not running**
   ```bash
   docker compose ps
   ```
   Should show `caddy` with status "Up". If not:
   ```bash
   docker compose up -d caddy
   ```

2. **Docker instance container is not running**
   ```bash
   docker compose ps
   ```
   Check if the instance container is listed and running. If not, the provisioner failed to start it.

3. **Firewall is blocking ports 80/443**
   ```bash
   # Check if ports are listening
   sudo netstat -tlnp | grep -E ':80|:443'
   ```
   Should show caddy listening on 0.0.0.0:80 and 0.0.0.0:443. If not, your firewall is blocking them. Adjust your VPS firewall rules.

4. **Caddy configuration is wrong**
   - Check the Caddyfile syntax:
     ```bash
     docker compose exec caddy caddy validate --config /etc/caddy/Caddyfile
     ```
   - If there are errors, fix the Caddyfile and restart

### "Too Many Certificates" Error

**Problem:** Let's Encrypt rejects your certificate request with a rate limit error

**Cause:** You've hit Let's Encrypt's rate limits (often happens during testing/debugging)

**Solution:** Use Let's Encrypt's staging server first to test, then switch to production when you're confident:

In your Caddyfile:

```
*.hotboxchat.ca {
    tls {
        dns namecheap {
            api_key {env.NAMECHEAP_API_KEY}
            user {env.NAMECHEAP_API_USER}
        }
        ca https://acme-staging-v02.api.letsencrypt.org/directory
    }
}
```

Test thoroughly, then remove the `ca` line to switch to production.

### Caddy Container Won't Start

**Problem:** `docker compose logs caddy` shows immediate exit or panic

**Possible causes:**

1. **Custom Caddy image not built**
   - Did you run `docker build -f Dockerfile.caddy ...`?
   - Check that the image exists: `docker images | grep caddy`

2. **Volume permissions issue**
   - Ensure `/etc/caddy/Caddyfile` exists and is readable:
     ```bash
     ls -la ./Caddyfile
     ```
   - If missing, create it with the content from Step 4

3. **Port already in use**
   - Another service is using port 80 or 443:
     ```bash
     sudo lsof -i :80
     sudo lsof -i :443
     ```
   - Either stop the other service or change Caddy's port in `docker-compose.yml`

## Alternative: Using Cloudflare DNS (Free)

If you can't enable the Namecheap API (due to account requirements), you can use **Cloudflare** as a free DNS provider instead. Cloudflare has an easier API setup and doesn't require IP whitelisting.

### Step 1: Move Your Domain's Nameservers to Cloudflare

1. Go to [cloudflare.com](https://www.cloudflare.com/) and sign up (free)
2. Add your domain (e.g., `hotboxchat.ca`)
3. Cloudflare will show you two nameservers like:
   ```
   sophia.ns.cloudflare.com
   nathan.ns.cloudflare.com
   ```
4. Go back to Namecheap → Domain List → Manage your domain
5. Click **Nameservers** tab
6. Select "Custom" and enter Cloudflare's two nameservers
7. Wait 5-10 minutes for Namecheap to update

### Step 2: Add DNS Records in Cloudflare

1. In Cloudflare dashboard, go to your domain
2. Click **DNS** in the sidebar
3. Add two A records (same as Step 2 of this guide):
   - Record 1: @ (root domain) → your server IP
   - Record 2: * (wildcard) → your server IP
4. Set both to "DNS only" (not Proxied)

### Step 3: Get Cloudflare API Token

1. In Cloudflare, click your profile icon (top right) → **API Tokens**
2. Click **Create Token**
3. Use the template "Edit zone DNS" and follow the prompts
4. Copy the token when it's generated

### Step 4: Update Caddyfile and Environment

In your Caddyfile:

```
*.hotboxchat.ca {
    tls {
        dns cloudflare {
            api_token {env.CLOUDFLARE_API_TOKEN}
        }
    }
    reverse_proxy localhost:8000
}

hotboxchat.ca {
    reverse_proxy localhost:8000
}
```

In your `.env`:

```bash
CLOUDFLARE_API_TOKEN=<your-cloudflare-api-token>
```

### Step 5: Update Caddy Docker Image

Use the Cloudflare plugin instead of Namecheap in your `Dockerfile.caddy`:

```dockerfile
FROM caddy:latest

RUN caddy build \
    --with github.com/caddy-dns/cloudflare
```

Rebuild and restart:

```bash
docker build -f Dockerfile.caddy -t caddy-cloudflare:latest .
docker compose up -d caddy
```

That's it! Cloudflare's DNS propagates quickly and the API setup is simpler. Follow **Step 5: Verify It Works** from the main guide to test.

---

## Next Steps

Once your DNS and wildcard certificates are working, you're ready to:
- Deploy the HotBox Provisioner app
- Create instances and verify traffic is routed correctly
- Monitor Caddy and your instances in production

See the provisioner's deployment guide for the next steps.
