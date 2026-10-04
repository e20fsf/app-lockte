# app-lockte

## Branching workflow

- `main` — production-ready code. Only updated by merging `develop` (releases) or `hotfix/*`.
- `develop` — integration branch. Default target for pull requests.
- `feature/<name>` — new work, branched from `develop`, merged back via PR.
- `fix/<name>` — non-urgent bug fixes, branched from `develop`.
- `hotfix/<name>` — urgent production fixes, branched from `main`, merged into both `main` and `develop`.

```bash
git switch develop
git pull
git switch -c feature/my-change
# ...commit...
git push -u origin feature/my-change
# open a PR into develop
```
