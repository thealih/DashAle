# PriceTracker.Back

## دیتابیس آزمایشی

در ویندوز، SQL Server Express LocalDB و ابزار `dotnet-ef` را نصب کنید. از داخل پوشهٔ `.Net` دستور زیر را اجرا کنید:

```powershell
dotnet ef database update --project .\PriceTracker.Infrastracture --startup-project .\PriceTracker.Api --context AppDbContext
```

مقصد پیش‌فرض `(localdb)\MSSQLLocalDB` و نام دیتابیس `PriceTrackerDev` است. برای مقصد دیگری مقدار `DB__SqlConnectionString` را در محیط تنظیم کنید. migration اولیه جدول‌ها، هفت برند و تنظیمات پیش‌فرض پایش را می‌سازد. حساب کاربری اولیه و داده‌های نسخهٔ پایتون در این migration وارد نمی‌شوند.

## ساخت حساب و اجرای API

بعد از اجرای migration، در PowerShell 7 از داخل `.Net` حساب مدیر را بسازید. رمز هنگام تایپ پنهان است و باید ۱۲ تا ۵۰۰ کاراکتر داشته باشد.

```powershell
Read-Host 'Password' -MaskInput | dotnet run --project .\PriceTracker.Api --launch-profile http -- --create-panel-user admin admin
dotnet run --project .\PriceTracker.Api --launch-profile http
```

سرویس توسعه روی `http://localhost:5125` اجرا می‌شود. برای ساخت حساب مشاهده‌گر، پارامترهای انتهایی را با نام کاربری موردنظر و نقش `viewer` جایگزین کنید. مسیر ورود `POST /api/auth/login` با بدنهٔ JSON شامل `username` و `password` است. مسیرهای داشبورد و کاتالوگ از کوکی ورود استفاده می‌کنند.

### JWT و دسترسی کاربران

ورود از `POST /api/auth/login` دو توکن صادر می‌کند. `access_token` به‌صورت پیش‌فرض ۱۵ دقیقه معتبر است و برای APIهای محافظت‌شده در هدر `Authorization: Bearer <token>` ارسال می‌شود. `refresh_token` تا پایان نشست، به‌صورت پیش‌فرض حداکثر ۷ روز از ورود، برای تمدید اعتبار دارد.

برای تمدید از `POST /api/auth/refresh` با بدنهٔ `{"refresh_token":"..."}` استفاده کنید. پاسخ شامل یک Access Token و یک Refresh Token جدید است. هر بار هر دو مقدار را جایگزین کنید. استفادهٔ دوباره از Refresh Token مصرف‌شده تمام نشست را باطل می‌کند. در مرورگر هر دو توکن در کوکی `HttpOnly` قرار می‌گیرند و پنل درخواست تمدید را خودکار می‌فرستد. خروج همان نشست را لغو می‌کند و تغییر نقش همهٔ نشست‌های حساب هدف را می‌بندد. در صورت انقضای Access Token، امکان خروج با Refresh Token نیز وجود دارد.

| مسیر | دسترسی |
| --- | --- |
| `GET /api/me`، داشبورد و مشاهدهٔ محصولات | مدیر و مشاهده‌گر |
| `POST /api/v1/users` | مدیر. بدنه شامل `username`، `password` و `role` است. نقش پیش‌فرض `viewer` است. |
| `GET /api/v1/users?q=ali&role=viewer&page=1&page_size=20` | مدیر. فیلترهای نام کاربری، نقش و وضعیت `active` اختیاری‌اند. |
| `PATCH /api/v1/users/{id}/role` | مدیر. بدنهٔ نمونه `{"role":"admin"}` است. سامانه حداقل یک مدیر فعال را حفظ می‌کند. |
| تغییر محصولات، پایش و کشف برند | مدیر |

در توسعه، اگر کلید تعیین نشود، سرویس برای هر بار اجرا کلید تصادفی می‌سازد. برای معتبر ماندن Access Tokenهای صادرشده بعد از راه‌اندازی دوباره، کلید را ثابت نگه دارید. نمونهٔ تولید کلید توسعه در PowerShell 7:

```powershell
$env:Jwt__SigningKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

برای Production، `DB__SqlConnectionString` و یک کلید ثابت و محرمانهٔ `Jwt__SigningKey` را با تنظیمات محیط استقرار وارد کنید. هر نصب مستقل باید کلید خودش را داشته باشد. `Jwt__Issuer`، `Jwt__Audience`، `Jwt__LifetimeMinutes` و `Jwt__RefreshLifetimeDays` هم از محیط قابل تنظیم‌اند.

### اجرای موتور پایش

برای فعال‌کردن موتور .NET روی دیتابیس توسعه یا یک دیتابیس آزمایشی مستقل، از داخل `.Net` اجرا کنید:

```powershell
$env:Monitoring__Enabled = 'true'
dotnet run --project .\PriceTracker.Api --launch-profile http
```

مقدار پیش‌فرض `Monitoring:Enabled` خاموش است تا روشن‌کردن API در دورهٔ انتقال، یک موتور پایش دیگر راه نیندازد. وقتی گزینه فعال است، سرور صف کشف و بررسی قیمت را هر ۱۰ ثانیه بررسی می‌کند. کشف موفق محصولات تازه را با پایش خاموش وارد کاتالوگ می‌کند. برای دیدن وضعیت صف از `GET /api/jobs` استفاده کنید. تنظیم فاصلهٔ پایش، آستانهٔ اعلان، مدت نگهداری تاریخچه و فاصلهٔ کشف مجدد از `GET /api/settings` و برای مدیر از `PUT /api/settings` در دسترس است.

هر بررسی موفق، آخرین قیمت‌ها، تاریخچه و رویدادهای تغییر را در یک تراکنش ذخیره می‌کند. در خرابی منبع، قیمت‌های قبلی حفظ می‌شوند و بررسی بعدی با تأخیر دوباره برنامه‌ریزی می‌شود. فروشنده‌ای که در پاسخ کامل بعدی دیده نشده، با وضعیت «دیده نشد» مشخص می‌شود. نگهداری تاریخچه طبق تعداد روزهای تنظیم‌شده، در دسته‌های محدود انجام می‌شود.

### اعلان‌ها، مقایسهٔ قیمت و اکسل

- `GET /api/notifications` اعلان‌ها را با فیلتر برند، محصول، فروشنده، نوع، تاریخ و وضعیت خواندن می‌دهد. `POST /api/notifications/read` با `ids` انتخابی، یا با بدنهٔ `{}` برای همهٔ اعلان‌های قابل مشاهده، وضعیت خواندن مشترک پنل را ثبت می‌کند.
- در `PUT /api/settings/own-store` مدیر فروشگاه پیش‌فرض و مقدار کاهش پیشنهادی را تعیین می‌کند. `GET /api/sellers` فروشگاه‌های شناخته‌شده را نشان می‌دهد. `GET /api/price-position` فقط قیمت‌های جدید و موجود برای گونه و گارانتی یکسان را مقایسه می‌کند. در صورت برابری قیمت، وضعیت «هم‌قیمت» برمی‌گردد. قیمت پیشنهادی برای تصمیم‌گیری مدیر است.
- `GET /api/reports/excel` خلاصه، آخرین قیمت‌ها، تغییرات و در صورت درخواست تاریخچه را صادر می‌کند. بازهٔ زمانی فقط تغییرات و تاریخچه را محدود می‌کند و انتهای آن انحصاری است. `GET /api/price-position/excel` با همان فیلترهای برند، جست‌وجو و وضعیت، خروجی مقایسه را می‌دهد.

فایل‌ها راست‌چین‌اند، سربرگ ثابت و فیلتر دارند و قیمت‌ها در سلول‌های عددی به تومان ذخیره می‌شوند. زمان‌ها به وقت تهران‌اند. اگر گزارش بیش از ۲۰۰٬۰۰۰ ردیف داشته باشد، سرویس خطای `413` می‌دهد تا بازه یا فیلتر محدود شود. سقف کمتر را می‌توان با `Reports__MaxRows` تنظیم کرد.



## Getting started

To make it easy for you to get started with GitLab, here's a list of recommended next steps.

Already a pro? Just edit this README.md and make it your own. Want to make it easy? [Use the template at the bottom](#editing-this-readme)!

## Add your files

- [ ] [Create](https://docs.gitlab.com/ee/user/project/repository/web_editor.html#create-a-file) or [upload](https://docs.gitlab.com/ee/user/project/repository/web_editor.html#upload-a-file) files
- [ ] [Add files using the command line](https://docs.gitlab.com/ee/gitlab-basics/add-file.html#add-a-file-using-the-command-line) or push an existing Git repository with the following command:

```
cd existing_repo
git remote add origin https://git.ictc.sharif.ir/PriceTracker/PriceTracker.Back.git
git branch -M main
git push -uf origin main
```

## Integrate with your tools

- [ ] [Set up project integrations](https://git.ictc.sharif.ir/PriceTracker/PriceTracker.Back/-/settings/integrations)

## Collaborate with your team

- [ ] [Invite team members and collaborators](https://docs.gitlab.com/ee/user/project/members/)
- [ ] [Create a new merge request](https://docs.gitlab.com/ee/user/project/merge_requests/creating_merge_requests.html)
- [ ] [Automatically close issues from merge requests](https://docs.gitlab.com/ee/user/project/issues/managing_issues.html#closing-issues-automatically)
- [ ] [Enable merge request approvals](https://docs.gitlab.com/ee/user/project/merge_requests/approvals/)
- [ ] [Set auto-merge](https://docs.gitlab.com/ee/user/project/merge_requests/merge_when_pipeline_succeeds.html)

## Test and Deploy

Use the built-in continuous integration in GitLab.

- [ ] [Get started with GitLab CI/CD](https://docs.gitlab.com/ee/ci/quick_start/index.html)
- [ ] [Analyze your code for known vulnerabilities with Static Application Security Testing (SAST)](https://docs.gitlab.com/ee/user/application_security/sast/)
- [ ] [Deploy to Kubernetes, Amazon EC2, or Amazon ECS using Auto Deploy](https://docs.gitlab.com/ee/topics/autodevops/requirements.html)
- [ ] [Use pull-based deployments for improved Kubernetes management](https://docs.gitlab.com/ee/user/clusters/agent/)
- [ ] [Set up protected environments](https://docs.gitlab.com/ee/ci/environments/protected_environments.html)

***

# Editing this README

When you're ready to make this README your own, just edit this file and use the handy template below (or feel free to structure it however you want - this is just a starting point!). Thanks to [makeareadme.com](https://www.makeareadme.com/) for this template.

## Suggestions for a good README

Every project is different, so consider which of these sections apply to yours. The sections used in the template are suggestions for most open source projects. Also keep in mind that while a README can be too long and detailed, too long is better than too short. If you think your README is too long, consider utilizing another form of documentation rather than cutting out information.

## Name
Choose a self-explaining name for your project.

## Description
Let people know what your project can do specifically. Provide context and add a link to any reference visitors might be unfamiliar with. A list of Features or a Background subsection can also be added here. If there are alternatives to your project, this is a good place to list differentiating factors.

## Badges
On some READMEs, you may see small images that convey metadata, such as whether or not all the tests are passing for the project. You can use Shields to add some to your README. Many services also have instructions for adding a badge.

## Visuals
Depending on what you are making, it can be a good idea to include screenshots or even a video (you'll frequently see GIFs rather than actual videos). Tools like ttygif can help, but check out Asciinema for a more sophisticated method.

## Installation
Within a particular ecosystem, there may be a common way of installing things, such as using Yarn, NuGet, or Homebrew. However, consider the possibility that whoever is reading your README is a novice and would like more guidance. Listing specific steps helps remove ambiguity and gets people to using your project as quickly as possible. If it only runs in a specific context like a particular programming language version or operating system or has dependencies that have to be installed manually, also add a Requirements subsection.

## Usage
Use examples liberally, and show the expected output if you can. It's helpful to have inline the smallest example of usage that you can demonstrate, while providing links to more sophisticated examples if they are too long to reasonably include in the README.

## Support
Tell people where they can go to for help. It can be any combination of an issue tracker, a chat room, an email address, etc.

## Roadmap
If you have ideas for releases in the future, it is a good idea to list them in the README.

## Contributing
State if you are open to contributions and what your requirements are for accepting them.

For people who want to make changes to your project, it's helpful to have some documentation on how to get started. Perhaps there is a script that they should run or some environment variables that they need to set. Make these steps explicit. These instructions could also be useful to your future self.

You can also document commands to lint the code or run tests. These steps help to ensure high code quality and reduce the likelihood that the changes inadvertently break something. Having instructions for running tests is especially helpful if it requires external setup, such as starting a Selenium server for testing in a browser.

## Authors and acknowledgment
Show your appreciation to those who have contributed to the project.

## License
For open source projects, say how it is licensed.

## Project status
If you have run out of energy or time for your project, put a note at the top of the README saying that development has slowed down or stopped completely. Someone may choose to fork your project or volunteer to step in as a maintainer or owner, allowing your project to keep going. You can also make an explicit request for maintainers.
