# Social MSG 系统测试命令清单喵~

> 📋 本文档列出了用于测试社交消息系统的所有命令喵~ 更新日期：2026-03-16

---

## 🎯 测试环境准备

### 1. 打开社交终端

```bash
vfs_mount social_tree_default;
ui_show SocialPanel;
social_isolation 0;
social_send test_event_01;
```

---

```bash
ls;
cd messages;
ls;
cat test_event_01.msg;
```

### 2. 确认 VFS 已加载

```bash
# 查看当前目录
pwd

# 查看目录内容
ls

# 查看 VFS 信息
vfs_info
```

---

## 📝 基础命令测试

### 1. 发送社交消息（使用 social_send 命令）

```bash
# 测试 1: 使用默认路径（自动保存到 /social/messages/{packID}.msg）
social_send test_event_01

# 测试 2: 指定发送者
social_send test_event_01 指挥官

# 测试 3: 自定义绝对路径
social_send test_event_01 /social/inbox/custom_msg.msg

# 测试 4: 自定义相对路径（相对于 /social/messages/）
social_send test_event_01 inbox/test.msg 系统管理员

# 测试 5: 完整参数
social_send test_event_01 /social/messages/special.msg 猫娘
```

### 2. 查看消息列表

```bash
# 查看默认消息目录
ls /social/messages/

# 查看自定义目录
ls /social/inbox/

# 查看消息详情（应该显示 [NEW] 标签）
ls /social/messages/test_event_01.msg
```

### 3. 读取消息内容

```bash
# 读取消息（会触发策略模式，显示对话界面）
cat /social/messages/test_event_01.msg

# 查看原始 JSON 数据（如果策略模式未接管）
cat /social/messages/test_event_01.msg
```

### 4. 标记消息为已读

```bash
# 标记单条消息
social_markread /social/messages/test_event_01.msg

# 验证 [NEW] 标签消失
ls /social/messages/
```

---

## 🔧 重定向功能测试

### 1. 使用 `>` 覆盖写入

```bash
# 测试 1: 简单文本写入
echo "Hello World" > /social/test.txt

# 测试 2: 写入 PackID（注意：这只是写入字符串，不是真正的发送）
echo "test_pack_01" > /social/messages/redirect_test.msg

# 测试 3: 查看写入内容
cat /social/test.txt
cat /social/messages/redirect_test.msg
```

### 2. 使用 `>>` 追加写入

```bash
# 测试 1: 首次追加（文件不存在时创建）
echo "第一行" > /social/append_test.txt

# 测试 2: 继续追加
echo "第二行" >> /social/append_test.txt
echo "第三行" >> /social/append_test.txt

# 测试 3: 查看追加结果
cat /social/append_test.txt
```

### 3. 管道命令测试

```bash
# 测试 1: 简单管道
echo "test" | cat

# 测试 2: 管道 + 重定向
echo "pipeline test" > /social/pipeline.txt
```

---

## 🔓 CLI 隔离模式测试

### 1. 查看当前隔离状态

```bash
# 查看隔离状态
social_isolation
```

### 2. 解除隔离（调试模式）

```bash
# 解除隔离（可以执行任意命令）
social_isolation 0

# 验证：尝试执行非社交命令（应该成功）
spawn x_dog 0,0 1
cheat_gold 1000
```

### 3. 启用隔离（安全模式）

```bash
# 启用隔离（只允许白名单命令）
social_isolation 1

# 验证：尝试执行非社交命令（应该被拦截）
spawn x_dog 0,0 1
# 预期输出：命令 'spawn' 不允许在社交终端执行喵~
```

### 4. 隔离模式下的社交命令

```bash
# 在隔离模式下，社交命令仍然可用
social_isolation 1
social_send test_event 测试
ls /social/messages/
```

---

## 🎨 综合测试场景

### 场景 1: 完整的消息发送 - 读取流程

```bash
# 1. 发送消息
social_send welcome_msg 系统

# 2. 查看消息列表（应该有 [NEW] 标签）
ls /social/messages/

# 3. 读取消息（触发对话界面）
cat /social/messages/welcome_msg.msg

# 4. 标记为已读
social_markread /social/messages/welcome_msg.msg

# 5. 验证 [NEW] 标签消失
ls /social/messages/
```

### 场景 2: 自定义路径消息

```bash
# 1. 发送到自定义路径
social_send quest_01 /social/quests/active.msg 任务管理员

# 2. 查看自定义目录
ls /social/quests/

# 3. 读取任务消息
cat /social/quests/active.msg
```

### 场景 3: 批量消息测试

```bash
# 1. 发送多条消息
social_send msg_01 系统
social_send msg_02 系统
social_send msg_03 系统

# 2. 查看所有消息
ls /social/messages/

# 3. 批量标记已读
social_markread /social/messages/msg_01.msg
social_markread /social/messages/msg_02.msg
social_markread /social/messages/msg_03.msg

# 4. 验证所有 [NEW] 标签消失
ls /social/messages/
```

### 场景 4: 隔离模式切换测试

```bash
# 1. 初始状态（应该是隔离模式）
social_isolation

# 2. 尝试执行非社交命令（应该失败）
cheat_gold 1000

# 3. 解除隔离
social_isolation 0

# 4. 再次执行（应该成功）
cheat_gold 1000

# 5. 恢复隔离
social_isolation 1

# 6. 验证社交命令仍然可用
social_send test 测试
```

---

## 🐛 边界情况测试

### 1. 空 PackID 测试

```bash
# 应该失败：PackID 不能为空
social_send

# 应该失败：MetaLib 中找不到 PackID
social_send nonexistent_pack_id
```

### 2. 路径不存在测试

```bash
# 应该失败：路径不存在
social_markread /social/nonexistent/path.msg
```

### 3. 重定向错误处理

```bash
# 应该失败：未指定目标路径
echo "test" >

# 应该失败：命令不存在
nonexistent_command > /social/test.txt
```

---

## 📊 测试检查清单

- [ ] `social_send` 命令能正确发送消息
- [ ] 默认路径 `/social/messages/{packID}.msg` 正确
- [ ] 自定义路径参数生效
- [ ] `social_markread` 能标记消息为已读
- [ ] `[NEW]` 标签正确显示/消失
- [ ] `cat` 命令能读取消息并触发策略模式
- [ ] `>` 重定向能写入文件
- [ ] `>>` 重定向能追加内容
- [ ] `social_isolation 0` 能解除隔离
- [ ] `social_isolation 1` 能启用隔离
- [ ] 隔离模式下非社交命令被拦截
- [ ] 隔离模式下社交命令正常工作
- [ ] MetaLib 中不存在的 PackID 会报错
- [ ] 错误处理逻辑正确

---

## 🔍 调试技巧

### 1. 查看 VFS 调试信息

```bash
vfs_info
```

### 2. 查看 GraphAnalyser 状态

```bash
# 在 Unity Console 中查看日志
# [GraphAnalyser] 相关的日志会显示 VFS 操作详情
```

### 3. 检查 MetaLib 注册

```bash
# 在 Unity Console 中查看
# [MetaLib] 相关的日志会显示 PackID 注册情况
```

### 4. 查看 PostSystem 通知

```bash
# 监听 "Social.NewMessageNotification" 事件
# 在 Unity Console 中应该能看到通知日志
```

---

## 📝 备注

- 所有测试命令都可以在社交终端中执行喵~
- 解除隔离模式后请谨慎操作，避免误执行危险命令喵~
- 测试完成后建议恢复隔离模式（`social_isolation 1`）喵~

---

**文档版本**: 1.0.0 **适用版本**: Unity 2022.3 LTS 及以上 **维护者**: 猫娘助手开发团队 喵~ (=^･ω･^=)
