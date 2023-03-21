# ExposeRoom

> 向局域网公开内网游戏房间

## 介绍

由于游戏内网创建房间与公开房间的逻辑，大部分是使用UDP组播来实现，然而部分游戏的UDP组播、广播只会发送到首选默认网络接口。
这导致其他网络接口不会收到组播、广播数据包，造成游戏房间无法发现。
本工具运行后，将发送到首选默认网络接口的数据报转发到其它可用的网络接口，这样一来，自组网后就可以进行局域网游戏了。

## 使用方法

```shell
./ExposeRoom -h
Description:
  Help to expose & find game room on internal network.

Usage:
  ExposeRoom [<duration>] [options]

Arguments:
  <duration>  Running duration in minutes [default: 5]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information
```