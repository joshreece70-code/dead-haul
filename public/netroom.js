/* Networking for the hosted build: one WebSocket per lobby code to this site's own /room/<code> endpoint.
   It exposes the same small room interface the game already uses, so the game code does not change. */
function makeWsRooms(WS,base){
  function mkRoom(name){
    let ws=null,myId=null,alive=true,up=false,tries=0,retryT=null;
    let mine={};const others={},hPeers=[],hConn=[];
    const list=()=>{const l=[{peer:myId||'me',sameTab:true,isMe:true,kind:'viewer',guest:false,by:null,presence:mine,updatedAt:0}];for(const k in others)l.push({peer:k,sameTab:false,isMe:false,kind:'viewer',guest:false,by:null,presence:others[k],updatedAt:0});return l;};
    const fire=()=>{const l=list();for(const h of hPeers.slice()){try{h({peers:l,joined:[],left:[],updated:[]});}catch(e){}}};
    const setUp=v=>{if(up===v)return;up=v;for(const h of hConn.slice()){try{h(v);}catch(e){}}};
    const push=()=>{if(ws&&ws.readyState===1&&myId){try{ws.send(JSON.stringify({t:'p',p:mine}));}catch(e){}}};
    function drop(){
      setUp(false);for(const k in others)delete others[k];fire();
      if(!alive||retryT)return;tries++;
      retryT=setTimeout(()=>{retryT=null;open();},Math.min(8000,500*tries)+Math.random()*500);
    }
    function open(){
      if(!alive)return;let s;
      try{s=new WS(base+name);}catch(e){drop();return;}
      ws=s;myId=null;
      s.onmessage=ev=>{
        if(s!==ws)return;let d;try{d=JSON.parse(ev.data);}catch(e){return;}
        if(!d)return;
        if(d.t==='hi'&&typeof d.id==='string'){myId=d.id;tries=0;setUp(true);push();fire();}
        else if(d.t==='s'&&d.p&&typeof d.p==='object'){for(const k in others)if(!(k in d.p))delete others[k];for(const k in d.p)if(k!==myId)others[k]=d.p[k];fire();}
      };
      s.onclose=()=>{if(s===ws){ws=null;drop();}};
      s.onerror=()=>{try{s.close();}catch(e){}};
    }
    open();
    return {
      name:name,
      presence(patch){mine=Object.assign({},mine);for(const k in patch){if(patch[k]===null)delete mine[k];else mine[k]=patch[k];}push();return Promise.resolve();},
      peers:list,
      onPeers(h){hPeers.push(h);setTimeout(()=>{if(hPeers.indexOf(h)>=0)fire();},0);return()=>{const i=hPeers.indexOf(h);if(i>=0)hPeers.splice(i,1);};},
      onConnection(h){hConn.push(h);setTimeout(()=>{try{h(up);}catch(e){}},0);return()=>{const i=hConn.indexOf(h);if(i>=0)hConn.splice(i,1);};},
      connected:()=>up,
      leave(){alive=false;if(retryT)clearTimeout(retryT);const s=ws;ws=null;try{if(s)s.close();}catch(e){}for(const k in others)delete others[k];return Promise.resolve();}
    };
  }
  const rooms={};
  const lobby=mkRoom('open');
  lobby.join=function(n){n=String(n);if(rooms[n])return Promise.resolve(rooms[n]);const r=mkRoom(n);const lv=r.leave;r.leave=function(){delete rooms[n];return lv();};rooms[n]=r;return Promise.resolve(r);};
  return lobby;
}
if(typeof window!=='undefined'&&!window.claude&&window.WebSocket&&/^https?:$/.test(location.protocol)){
  const r=makeWsRooms(window.WebSocket,(location.protocol==='https:'?'wss://':'ws://')+location.host+'/room/');
  window.claude={use:async function(n){return n==='room'?r:null;}};
}
if(typeof module!=='undefined')module.exports={makeWsRooms};
