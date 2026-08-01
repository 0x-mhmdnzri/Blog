# BlogApp → AVICRM rename

## Done on branch `rename-avicrm`

- Namespace: `BlogApp` → `AVICRM`
- Project folder: `BlogApp/` → `AVICRM/`
- Project file: `AVICRM.csproj`
- Solution: `AVICRM.sln`
- Assembly: `AVICRM.dll`
- Dockerfile / compose paths updated

## Local apply (if you prefer offline)

```bash
git fetch origin rename-avicrm
git checkout rename-avicrm
dotnet build AVICRM.sln
```

## Rename GitHub repository

GitHub Settings → General → Repository name → **AVICRM**  
(or: `gh repo rename AVICRM`)

Update remote:

```bash
git remote set-url origin https://github.com/0x-mhmdnzri/AVICRM.git
```

## Cleanup

After merge, remove leftover `BlogApp/` directory and `BlogApp.sln` if still present.
