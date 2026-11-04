// WeChatShare.jslib —— 微信小游戏转发桥（C# 侧见 Assets/Scripts/Platform/WeChatShare.cs）
// 仅微信小游戏（WebGL）构建会被合并进包；编辑器/安卓构建不包含本文件，不影响现有开发包。
//
// 调用的都是微信运行时全局 `wx` 的官方接口（非编造）：
//   wx.showShareMenu({ withShareTicket })   打开右上角菜单的「转发」
//   wx.onShareAppMessage(fn)                设置被动转发内容（玩家自己点右上角转发时用）
//   wx.shareAppMessage(opt)                 主动拉起转发（有按钮入口时才用）
//   wx.getFileSystemManager().writeFileSync 把封面图写进沙箱（imageUrl 只认文件路径/网络图，不认 base64）
//
// 【为什么封面要写盘】微信 imageUrl 支持「网络图 / 本地文件 / 代码包内相对路径」，
// 但团结构建的小游戏里 Resources 图被打进资源包、不是包内独立文件 → 只能运行时把 JPG 写进
// wx.env.USER_DATA_PATH，再把该路径当 imageUrl 传。写盘失败就退化为「不传图」（微信用小游戏默认图）。
//
// 【口径】wx.shareAppMessage 的 success 只代表「调起了转发界面」，微信 2021 年后取消「是否真的发出去」的判定，
// 所以本桥只回传调起成败 —— 不做「分享成功发奖」那套（那套本身也违反运营规范 2.5.1）。
mergeInto(LibraryManager.library, {
  // 【2026-11-04 二次修复】本 jslib 里**不能出现任何自定义跨函数符号**：
  //   · `$WXShareLib`（库内部变量）：团结 framework 打包不会把它提升为模块作用域变量 → 运行时 undefined；
  //   · `WXShareLib: function(){}`（普通库函数）：Emscripten 生成的是 `_WXShareLib`，
  //     且因无 C# 导入引用被死代码剔除 → 调用处原样留下的 `WXShareLib()` 依旧 undefined。
  // 两者都会让首场景 AfterSceneLoad 抛异常，被 callMain 吞掉 → setMainLoop 不执行 →
  // 下一帧 `Browser.mainLoop.scheduler is not a function` → 黑屏（13:29 / 13:42 两次真机+模拟器实锤）。
  // 现把状态直接挂在框架内建的 `Module` 上，各函数入口自行惰性初始化，不依赖任何自定义符号。
  // 初始化表达式（三处一致，勿抽成函数/变量）：
  //   Module.__wxShare || (Module.__wxShare = { title, query, coverPath, build() })

  // 设置右上角菜单转发（幂等，可重复调；标题/query 以最后一次为准）
  WXShareSetupMenu: function (titlePtr, queryPtr) {
    var lib;
    try {
      lib = Module.__wxShare || (Module.__wxShare = {
        title: '',
        query: '',
        coverPath: '',
        build: function () {
          var opt = {};
          if (this.title) opt.title = this.title;
          if (this.query) opt.query = this.query;
          if (this.coverPath) opt.imageUrl = this.coverPath;
          return opt;
        }
      });
      lib.title = UTF8ToString(titlePtr);
      lib.query = queryPtr ? UTF8ToString(queryPtr) : '';
      wx.showShareMenu({ withShareTicket: true });
      wx.onShareAppMessage(function () { return lib.build(); });
      return 1;
    } catch (e) {
      console.error('[WeChatShare] 转发菜单设置失败', e && (e.errMsg || e.message || e.stack) || String(e));
      return 0;
    }
  },

  // 把封面 JPG 字节写进沙箱，路径记到 Module.__wxShare.coverPath（之后转发自动带上）
  WXShareSetCover: function (ptr, len) {
    if (typeof wx === 'undefined' || !wx.getFileSystemManager || !wx.env) return 0;
    try {
      // slice() 复制一份：HEAPU8 视图会在内存增长时失效，不能直接把视图交给异步 API
      var bytes = new Uint8Array(HEAPU8.buffer, ptr, len).slice();
      var path = wx.env.USER_DATA_PATH + '/share_cover.jpg';
      wx.getFileSystemManager().writeFileSync(path, bytes.buffer, 'binary');
      var lib = Module.__wxShare || (Module.__wxShare = {
        title: '', query: '', coverPath: '',
        build: function () {
          var opt = {};
          if (this.title) opt.title = this.title;
          if (this.query) opt.query = this.query;
          if (this.coverPath) opt.imageUrl = this.coverPath;
          return opt;
        }
      });
      lib.coverPath = path;
      return 1;
    } catch (e) {
      console.error('[WeChatShare] 封面写沙箱失败', e && (e.errMsg || e.message || e.stack) || String(e));
      return 0;
    }
  },

  // 主动拉起转发；onDone(1/0) = 调起成功/失败（不代表玩家真的发出去了）
  WXShareAppMessage: function (onDone) {
    var done = function (ok) { if (onDone) Module.dynCall_vi(onDone, ok ? 1 : 0); };
    if (typeof wx === 'undefined' || !wx.shareAppMessage) {
      console.warn('[WeChatShare] wx.shareAppMessage 不可用');
      done(false);
      return;
    }
    try {
      var lib = Module.__wxShare || (Module.__wxShare = {
        title: '', query: '', coverPath: '',
        build: function () {
          var opt = {};
          if (this.title) opt.title = this.title;
          if (this.query) opt.query = this.query;
          if (this.coverPath) opt.imageUrl = this.coverPath;
          return opt;
        }
      });
      wx.shareAppMessage(lib.build());
      done(true);
    } catch (e) {
      console.error('[WeChatShare] 主动转发失败', e && (e.errMsg || e.message || e.stack) || String(e));
      done(false);
    }
  }
});
