# Security Policy

## Supported versions
CanvasLab v0.1.0-beta.1 is a public beta. Security fixes will be assessed for supported beta releases.

## Reporting a vulnerability
Please do not post exploit details or private images in public issues. Use GitHub's private vulnerability reporting feature if enabled, or contact the repository owner through an appropriate private channel.

## Security approach
CanvasLab is intended to process images locally. Image files are untrusted inputs; parsing and native image-codec dependencies require particular care. The app must not upload images, introduce telemetry, or overwrite originals without explicit user approval.
