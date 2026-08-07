# OG / Social share cards

Already implemented:
- PostOgCardService: 1200x630 GitHub-style cards (category, lang, title, summary, tags, views, likes, read, date)
- Author + site cards
- /og/post/{id}.png|.jpg endpoints
- Layout og:* + twitter:* tags

Enhancements applied in _Layout.cshtml:
- html prefix for Telegram
- dual og:image (PNG + JPEG for WhatsApp)
- profile:username when og:type=profile
- og:locale:alternate for fa/en/ar
