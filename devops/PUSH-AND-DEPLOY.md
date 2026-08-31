# Push + Deploy — یک‌صفحه‌ای (مثل سورس مرجع)

**مسیر اصلی:** commit → push GitHub → deploy خودکار روی سرور (self-hosted runner).  
Mac لازم نیست.

---

## ★ دستور روزمره (پیشنهادی)

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually

# ۱) commit در ریپویی که عوض شده (API / Admin / Public)
# ۲) یکی از این دو:

bash devops/scripts/push-and-deploy.sh              # prod + push + watch (~۱۴ min)
bash devops/scripts/push-and-deploy.sh --auto       # فقط ریپوهایی که commit جلوتر از origin دارند
```

معادل دستی (همان کار):

```bash
bash devops/scripts/gh-deploy-production.sh --prod --push --watch
```

یا فقط:

```bash
git push origin main   # در همان ریپوی تغییرکرده → CI/CD خودکار
```

---

## چه تغییری → چه push کنم؟

| تغییر در | ریپo | دستور سریع | زمان |
|----------|------|------------|------|
| C# / API | `Api_Vapp_Manually` | `push-and-deploy.sh --api` | **~۱۴ min** |
| پنل ادمین | `Admin_Vapp` | `push-and-deploy.sh --admin` | **~۲ min** |
| فرم/گردونه Public | `Public_Vapp` | `push-and-deploy.sh --public` | **~۱–۲ min** |
| همه (prod) | هر سه | `push-and-deploy.sh` | **~۱۴ min** (موازی) |
| Scraper | `scraping_Number_Vapp` | `push-and-deploy.sh --scraper` | **~۳۰ min** |

---

## قدم‌به‌قدم (ساده)

```
۱) کد را commit کن (اسکریپت commit نمی‌کند)
        ↓
۲) push-and-deploy.sh  (یا git push)
        ↓
۳) CI روی GitHub (test / build)
        ↓
۴) [اختیاری] Approve در GitHub → Review deployments
        ↓
۵) Deploy روی VPS (runner داخل سرور)
        ↓
۶) ✅ production live
```

جزئیات: [`DEPLOY-FLOW.md`](DEPLOY-FLOW.md)

---

## چرا API (~۱۴ min) بیشتر از فرانت (~۲ min)?

| | **فرانت (Admin/Public)** | **بکند (API)** |
|---|--------------------------|----------------|
| خروجی | فایل static کوچک (~۲–۵ MB) | Docker image (~۱۰۰+ MB فشرده) |
| build | Vite ~۳۰–۴۵ sec | dotnet test + docker build ~۴ min |
| انتقال به سرور | دانلود dist + rsync محلی ~۳۰ sec | **دانلود artifact از GitHub به VPS ایران ~۹ min** ← گلوگاه |
| restart | nginx reload | docker load + restart + migration SQL |

**خلاصه:** فرانت «فقط فایل» است؛ API «image سنگین Docker» است که باید از GitHub به ایران بیاید.

جزئیات: [`DEPLOY-TIMING.md`](DEPLOY-TIMING.md)

---

## Mac — وقتی عجله داری (بدون GitHub)

```bash
bash devops/scripts/push-and-deploy.sh --mac admin    # ~۲–۴ min
bash devops/scripts/push-and-deploy.sh --mac api      # ~۴–۷ min
```

یا:

```bash
bash devops/scripts/deploy-from-mac.sh admin|public|api
```

---

## بعد از deploy — چک سلامت

```bash
bash devops/scripts/post-deploy-verify.sh
# یا روی سرور:
ssh vapp-prod 'bash /root/Api_Vapp_Manually/devops/scripts/post-deploy-verify.sh'
```

---

## لینک Actions

| سرویس | URL |
|--------|-----|
| API | https://github.com/seyedWebpro/Api_Vapp_Manually/actions |
| Admin | https://github.com/seyedWebpro/Admin_Pannel_Vapp/actions |
| Public | https://github.com/seyedWebpro/PublicWeb_Vapp/actions |
| Scraper | https://github.com/seyedWebpro/scraping_Number_Vapp/actions |

---

## فایل‌های مرتبط

- [`COMMANDS.txt`](COMMANDS.txt) — همه دستورات
- [`CI_CD_QUICK.md`](CI_CD_QUICK.md) — CI/CD یک‌صفحه‌ای
- [`MAC-QUICK-DEPLOY.md`](MAC-QUICK-DEPLOY.md) — Mac hotfix
- [`WHAT-WAS-DONE.md`](WHAT-WAS-DONE.md) — تاریخچه setup
