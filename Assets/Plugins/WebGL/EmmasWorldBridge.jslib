// Bridges Unity's WebGL build to the hosting page's already-authenticated
// socket.io-client instance (see SocketProvider.tsx in the web app), which
// the page is expected to expose as window.__emmasWorldSocket once
// connected. Block placement/removal do NOT go through this bridge -- those
// are plain UnityWebRequest calls to the REST API from C#, since same-origin
// requests carry the auth cookie automatically. This bridge only handles the
// live Socket.io connection: joining/leaving the shared world, sending this
// client's position, and relaying incoming events back into Unity.
mergeInto(LibraryManager.library, {

  EmmasWorld_IsReady: function () {
    var socket = window.__emmasWorldSocket;
    return (socket && socket.connected) ? 1 : 0;
  },

  EmmasWorld_RegisterListeners: function () {
    var socket = window.__emmasWorldSocket;
    if (!socket || socket.__emmasWorldListenersRegistered) return;
    socket.__emmasWorldListenersRegistered = true;

    socket.on('emmasworld:move', function (data) {
      SendMessage('MultiplayerManager', 'OnRemoteMove', JSON.stringify(data));
    });
    socket.on('emmasworld:snapshot', function (data) {
      // JsonUtility can't parse a bare JSON array, so wrap it.
      SendMessage('MultiplayerManager', 'OnSnapshot', JSON.stringify({ items: data }));
    });
    socket.on('emmasworld:user_left', function (data) {
      SendMessage('MultiplayerManager', 'OnUserLeft', JSON.stringify(data));
    });
    socket.on('emmasworld:block_placed', function (data) {
      SendMessage('MultiplayerManager', 'OnBlockPlaced', JSON.stringify(data));
    });
    socket.on('emmasworld:block_removed', function (data) {
      SendMessage('MultiplayerManager', 'OnBlockRemoved', JSON.stringify(data));
    });
  },

  EmmasWorld_JoinWorld: function () {
    var socket = window.__emmasWorldSocket;
    if (!socket) return;
    socket.emit('emmasworld:join');
  },

  EmmasWorld_LeaveWorld: function () {
    var socket = window.__emmasWorldSocket;
    if (!socket) return;
    socket.emit('emmasworld:leave');
  },

  EmmasWorld_SendMove: function (x, y, z, rotationY) {
    var socket = window.__emmasWorldSocket;
    if (!socket) return;
    socket.emit('emmasworld:move', { x: x, y: y, z: z, rotation_y: rotationY });
  },

});
