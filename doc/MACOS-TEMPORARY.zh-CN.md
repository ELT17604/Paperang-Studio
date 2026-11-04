# macOS 终端临时体验

[English](../Server/macOS-Temporary/README.md) · [服务端说明](REMOTE-PRINTING.zh-CN.md)

项目中已新增`Server/macOS-Temporary/`。在项目根目录的终端运行：

```sh
bash Server/macOS-Temporary/run.sh
```

也可以在Finder中双击`Server/macOS-Temporary/start.command`。需要保留完整的Server目录；入口复用上级的run.py、relay.py和web/。

需要macOS和Python3.10+，不需要sudo或pip依赖。入口会查找PATH和Apple Silicon/Intel的Homebrew常见路径。也可显式指定：

```sh
PAPERANG_PYTHON=/opt/homebrew/bin/python3 bash Server/macOS-Temporary/run.sh --port 9088
```

默认浏览器地址`http://127.0.0.1:8177`。终端显示临时设备密钥与投稿密钥；将地址和设备密钥填入Mac App的“远程打印”。访客可选择匿名投稿或使用投稿密钥识别身份。

## 可以附加的参数

参数放在run.sh或start.command后，全部转交共享临时服务处理：

| 参数 | 默认值 | 作用 |
|---|---|---|
| `--port N` |8177 |端口，1024–65535 |
| `--bind IPV4` |127.0.0.1 |监听地址；0.0.0.0允许局域网连接 |
| `--device-id ID` |p2 |1–64位英文字母/数字/下划线/连字符 |
| `--anonymous-max-length N` |500 |匿名每条字符数，1–10000 |
| `--anonymous-hourly-limit N` |5 |匿名每IP每小时条数，正整数 |
| `--anonymous-hourly-chars N` |2000 |匿名每IP每小时字符总量，正整数 |
| `--authenticated-max-length N` |10000 |鉴权每条字符数，1–10000 |
| `--authenticated-hourly-limit N` |60 |鉴权每IP每小时条数，正整数 |
| `--authenticated-hourly-chars N` |60000 |鉴权每IP每小时字符总量，正整数 |
| `--disable-anonymous` |关闭 |要求投稿密钥 |
| `--trusted-proxy CIDR` |无 |可信转发代理，可重复指定多个CIDR |
| `--dry-run` |关闭 |只校验和显示配置，不监听、不生成密钥 |
| `--help` / `-h` |— |显示参数帮助 |

IP配额在每个UTC整点小时重置。

```sh
# 自定义端口、字数与额度
bash Server/macOS-Temporary/run.sh \
  --port 9088 --bind 127.0.0.1 --device-id p2 \
  --anonymous-max-length 300 --anonymous-hourly-limit 3 \
  --anonymous-hourly-chars 900 --authenticated-hourly-limit 30

# 仅允许持密钥投稿
bash Server/macOS-Temporary/run.sh --port 9088 --disable-anonymous

# 预演／帮助
bash Server/macOS-Temporary/run.sh --port 9088 --dry-run
bash Server/macOS-Temporary/run.sh --help
```

局域网测试可加`--bind 0.0.0.0`，其他设备通过这台Mac的局域网IP和端口访问。0.0.0.0是监听地址，不是浏览器访问地址；macOS可能询问是否允许传入连接。程序不会替你修改防火墙。

## 停止与数据生命周期

保持终端开启；Ctrl-C或正常关闭终端（SIGHUP）即停止并清理临时数据库/密钥。不注册launchd/systemd服务，不会后台常驻。重新启动后使用全新密钥，原收件箱不保留；强制kill可能残留系统临时目录。

Ubuntu持久化安装仍使用`sudo bash Server/install.sh`，其`--prefix`、`--config`、`--state-dir`等安装专用参数不能传给macOS临时入口。[服务端总说明](../Server/README.md#available-parameters)列出完整参数及适用模式。
