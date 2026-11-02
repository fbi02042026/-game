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
  // $ 前缀 = 库内部变量（不导出给 C#），函数里直接写 WXShareLib 引用
  $WXShareLib: {
    title: '',
    query: '',
    coverPath: '',

    // 组装转发参数（title 必填；query / imageUrl 有才带）
    build: function () {
      var opt = {};
      if (this.title) opt.title = this.title;
      if (this.query) opt.query = this.query;
      if (this.coverPath) opt.imageUrl = this.coverPath;
      return opt;
    }
  },

  // 设置右上角菜单转发（幂等，可重复调；标题/query 以最后一次为准）
  WXShareSetupMenu: function (titlePtr, queryPtr) {
    if (typeof wx === 'undefined') {
      console.warn('[WeChatShare] 非微信运行时，跳过转发菜单设置');
      return 0;
    }
    WXShareLib.title = UTF8ToString(titlePtr);
    WXShareLib.query = queryPtr ? UTF8ToString(queryPtr) : '';
    try {
      wx.showShareMenu({ withShareTicket: true });
      wx.onShareAppMessage(function () { return WXShareLib.build(); });
      return 1;
    } catch (e) {
      console.error('[WeChatShare] 转发菜单设置失败', e);
      return 0;
    }
  },

  // 把封面 JPG 字节写进沙箱，路径记到 WXShareLib.coverPath（之后转发自动带上）
  WXShareSetCover: function (ptr, len) {
    if (typeof wx === 'undefined' || !wx.getFileSystemManager || !wx.env) return 0;
    try {
      // slice() 复制一份：HEAPU8 视图会在内存增长时失效，不能直接把视图交给异步 API
      var bytes = new Uint8Array(HEAPU8.buffer, ptr, len).slice();
      var path = wx.env.USER_DATA_PATH + '/share_cover.jpg';
      wx.getFileSystemManager().writeFileSync(path, bytes.buffer, 'binary');
      WXShareLib.coverPath = path;
      return 1;
    } catch (e) {
      console.error('[WeChatShare] 封面写沙箱失败', e);
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
      wx.shareAppMessage(WXShareLib.build());
      done(true);
    } catch (e) {
      console.error('[WeChatShare] 主动转发失败', e);
      done(false);
    }
  }
});
