# ESurfingDialerManager

## 项目描述

- 程序启动时若已连接天翼宽带Wi-Fi，则启动`ESurfingDialer-1.9.5-all.jar`以认证天翼账户。
- 当Wi-Fi从天翼宽带断开时，则自动中止ESurfingDialer-1.9.5-all.jar进程。

## 安装步骤

- 安装Java jre，jre版本由项目ESurfingDialer决定。
- 下载`ESurfingDialer-1.9.5-all.jar`以及本项目可执行文件。
- 复制`ESurfingDialerManager.json`、`log4j.properties`配置文件到`ESurfingDialer-1.9.5-all.jar`相同目录。
- 修改配置文件`ESurfingDialerManager.json`，填充天翼宽带账户和密码。
- 执行`ESurfingDialerManager.exe C:\path\to\ESurfingDialer\ESurfingDialerManager.json`

执行最后一步后，可打开任务管理器杀死`Java.exe`进程，断开连接其它Wi-Fi，然后重连到天翼宽带Wi-Fi。
确认一切功能正常后，可以将最后一步的命令添加到任务计划中实现开机启动。

## 配置文件

说明：`JarFile`、`Log4jConfiguration`可以使用相对路径的前提条件是`ESurfingDialerManager.json`与它们位于同一目录。
否则必须使用绝对路径。如果在环境变量`PATH`中无法找到`Java.exe`，请使用完整路径名。

```json
{
  "Java" : "java.exe",
  "ProfileName": "YONG",
  "User": "",
  "Password": "",
  "JarFile": "ESurfingDialer-1.9.5-all.jar",
  "Log4jConfiguration": "log4j.properties"
}
```