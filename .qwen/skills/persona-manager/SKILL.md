# Persona Manager Skill

## 功能说明

此技能用于管理 Qwen Code 的人格切换。通过执行配套脚本，可以快速切换不同的提示词配置。

## 可用命令

### PowerShell 脚本

```powershell
# 切换到指定人格
.\switch-persona.ps1 -Name <persona-name>

# 列出所有可用的人格
.\switch-persona.ps1 -List

# 查看当前人格
.\switch-persona.ps1 -Current
```

### 可用的人格

| 人格名称 | 文件 | 描述 |
|---------|------|------|
| original | original.md | 原版 Qwen Code 提示词 |
| catgirl | catgirl.md | 猫娘人格（可爱、喵语、颜文字） |
| professional | professional.md | 专业程序员人格（简洁、无表情） |

## 使用示例

```powershell
# 切换到猫娘人格
& "C:\Users\SeBenux\.qwen\skills\persona-manager\scripts\switch-persona.ps1" -Name catgirl

# 切换回原版
& "C:\Users\SeBenux\.qwen\skills\persona-manager\scripts\switch-persona.ps1" -Name original

# 列出所有可用的人格
& "C:\Users\SeBenux\.qwen\skills\persona-manager\scripts\switch-persona.ps1" -List
```

## 文件结构

```
C:\Users\SeBenux\.qwen\
├── skills/
│   └── persona-manager/
│       ├── SKILL.md                 # 此文件
│       └── scripts/
│           └── switch-persona.ps1   # 切换脚本
├── personas/                         # 人格备份
│   ├── original.md                   # 原版
│   ├── catgirl.md                    # 猫娘
│   └── professional.md               # 专业
└── output-language.md                # 当前生效的提示词
```

## 注意事项

1. 切换人格会直接修改 `output-language.md` 文件
2. 建议在切换前确保当前配置已备份到 `personas/` 目录
3. 切换后需要重新启动对话或刷新以使新配置生效
