---
sidebar_position: 1
---

# Welcome to VoiceByAuribus API

VoiceByAuribus is a professional voice conversion and audio processing API that enables you to:

- 🎤 **Access Voice Models**: Choose from our library of professional voice models
- 🎵 **Upload Audio Files**: Securely upload your audio files for processing
- 🔄 **Voice Conversion**: Transform audio using high-quality voice models with pitch shifting
- 🔔 **Real-time Notifications**: Receive instant updates via webhooks when conversions complete

## Quick Links

- **[Quickstart Guide](getting-started/quickstart)**: Get started in 5 minutes
- **[Authentication](getting-started/authentication)**: Learn about OAuth 2.0 authentication
- **[API Reference](api/voicebyauribus-api)**: Interactive API documentation
- **[Webhook Guide](guides/webhooks)**: Integrate webhook notifications

## Architecture

VoiceByAuribus API is built with:

- **.NET 10.0**: Modern, high-performance framework
- **PostgreSQL**: Robust relational database
- **Cloud-native Infrastructure**: Object storage, message queues, serverless functions
- **Vertical Slice Architecture**: Clean, maintainable codebase

## Authentication

The API uses **OAuth 2.0 Client Credentials** for secure machine-to-machine authentication. All requests must include a valid access token in the Authorization header.

[Learn more about authentication →](getting-started/authentication)

## API Endpoint

The production API is available at:

```
https://api.auribus.io
```

All API requests must be made over HTTPS. Requests made over plain HTTP will fail.

## Getting Help

Need assistance? We're here to help:

- 📧 **Email**: [support@auribus.io](mailto:support@auribus.io)
- 🌐 **Website**: [https://auribus.io](https://auribus.io)

## What's Next?

Ready to get started? Follow our [Quickstart Guide](getting-started/quickstart) to make your first API call in minutes.
