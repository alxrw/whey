# Whey

Whey is a backend registry service for [Parm](https://github.com/yhoundz/parm). It currently does the following:
- Acts as a mirror for Parm clients to install packages from Whey instead of GitHub
- Tracks install statistics/analytics for repositories
- Pre-computes client-side operations such as dependency finding and release asset selection.
- Allows expansion of parm into other vendors (e.g. Docker, GitLab)
- ...Is an exercise in backend development

## Running Whey

Before running, ensure you have Docker and Docker Compose installed.

Run `docker-compose up --build`
