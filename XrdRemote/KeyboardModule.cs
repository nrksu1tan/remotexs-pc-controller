using System;
using System.Runtime.InteropServices;

namespace XrdRemote
{
    public static class KeyboardModule
    {
        // --- WINAPI STRUCTS ---
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion U; public static int Size => Marshal.SizeOf(typeof(INPUT)); }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        const int INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_SCANCODE = 0x0008;

        public static void HandleCommand(string cmd, int keyCode, bool isDown)
        {
            if (cmd == "key_event")
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].U.ki.wVk = 0; 
                inputs[0].U.ki.wScan = (ushort)MapVirtualKey((uint)keyCode, 0);
                uint flags = KEYEVENTF_SCANCODE;
                if (!isDown) flags |= KEYEVENTF_KEYUP;
                if (IsExtendedKey(keyCode)) flags |= KEYEVENTF_EXTENDEDKEY;
                inputs[0].U.ki.dwFlags = flags;
                SendInput(1, inputs, INPUT.Size);
            }
        }

        private static bool IsExtendedKey(int keyCode)
        {
            return (keyCode >= 33 && keyCode <= 46) || keyCode == 111 || keyCode == 108 || keyCode == 91; 
        }

        public static string GetHtml()
        {
            return @"<!DOCTYPE html>
<html lang='ru'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
    <title>XrdKey Ultra</title>
    <style>
        :root { 
            --bg: #000;
            --k-main: #333;
            --k-dark: #222;
            --k-num: #1a1a1a;
            --act: #00E676;
            --txt: #ddd;
        }
        
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; -webkit-touch-callout: none; user-select: none; }

        body { 
            margin: 0; padding: 0; background: var(--bg); color: var(--txt);
            overflow: hidden; width: 100vw; height: 100dvh;
            font-family: sans-serif; touch-action: none;
        }
        
        #rotator {
            position: absolute; top: 50%; left: 50%;
            width: 100dvh; height: 100vw;
            transform: translate(-50%, -50%) rotate(90deg);
            display: flex; flex-direction: column; padding: 2px;
        }

        .bar { 
            display: flex; justify-content: space-between; height: 24px; margin-bottom: 2px;
            align-items: center; padding: 0 5px;
        }
        .btn-x { background: #444; color: white; text-decoration: none; padding: 2px 10px; font-size: 10px; border-radius: 2px; }

        .kb { flex: 1; display: flex; flex-direction: column; gap: 1px; }
        .row { display: flex; gap: 1px; flex: 1; }
        
        .k {
            flex: 1; background: var(--k-main);
            display: flex; align-items: center; justify-content: center;
            font-size: 13px; font-weight: bold; border-radius: 1px;
            position: relative;
        }
        
        .k:active, .k.on { background: var(--act) !important; color: black; }
        
        /* Цветовые зоны */
        .k.sys { background: #151515; font-size: 10px; color: #999; } /* F1-F12 */
        .k.num { background: var(--k-num); border-left: 1px solid #444; } /* Numpad справа */
        .k.mod { background: #222; font-size: 11px; text-transform: uppercase; } /* Shift, Ctrl */
        .k.arr { background: #1c1c1c; font-size: 16px; } /* Стрелки */
        .k.ent { background: #26332a; color: var(--act); } /* Enter */
        .k.esc { background: #331a1a; color: #ff5555; }
        
        .k.spc { flex: 4; } /* Пробел */

    </style>
</head>
<body>
    <div id='rotator'>
        <div class='bar'>
            <a href='/' class='btn-x'>EXIT</a>
            <span style='font-size:10px; color:#555'>PRO LAYOUT (NO GAPS)</span>
        </div>
        <div class='kb' id='kb'></div>
    </div>

    <script>
        document.addEventListener('gesturestart', e => e.preventDefault());
        
        // s: style class, w: width ratio
        const L = [
            // ROW 1: ESC, F-KEYS, DEL, NUM-TOP
            [
                {t:'Esc',c:27,s:'esc'}, {t:'F1',c:112,s:'sys'}, {t:'F2',c:113,s:'sys'}, {t:'F3',c:114,s:'sys'}, {t:'F4',c:115,s:'sys'}, {t:'F5',c:116,s:'sys'}, {t:'F6',c:117,s:'sys'}, {t:'F7',c:118,s:'sys'}, {t:'F8',c:119,s:'sys'}, {t:'F9',c:120,s:'sys'}, {t:'F10',c:121,s:'sys'}, {t:'F11',c:122,s:'sys'}, {t:'F12',c:123,s:'sys'}, {t:'Del',c:46,s:'esc'}, 
                {t:'Num',c:144,s:'num'}, {t:'/',c:111,s:'num'}, {t:'*',c:106,s:'num'}, {t:'-',c:109,s:'num'}
            ],
            // ROW 2: NUMBERS, BACK, NUM-789+
            [
                {t:'~',c:192}, {t:'1',c:49}, {t:'2',c:50}, {t:'3',c:51}, {t:'4',c:52}, {t:'5',c:53}, {t:'6',c:54}, {t:'7',c:55}, {t:'8',c:56}, {t:'9',c:57}, {t:'0',c:48}, {t:'-',c:189}, {t:'=',c:187}, {t:'Bksp',c:8,w:1.5,s:'mod'},
                {t:'7',c:103,s:'num'}, {t:'8',c:104,s:'num'}, {t:'9',c:105,s:'num'}, {t:'+',c:107,s:'num'}
            ],
            // ROW 3: QWERTY, NUM-456
            [
                {t:'Tab',c:9,w:1.2,s:'mod'}, {t:'Q',c:81}, {t:'W',c:87}, {t:'E',c:69}, {t:'R',c:82}, {t:'T',c:84}, {t:'Y',c:89}, {t:'U',c:85}, {t:'I',c:73}, {t:'O',c:79}, {t:'P',c:80}, {t:'[',c:219}, {t:']',c:221}, {t:'\\',c:220},
                {t:'4',c:100,s:'num'}, {t:'5',c:101,s:'num'}, {t:'6',c:102,s:'num'}, {t:'+',c:107,s:'num'} 
            ],
            // ROW 4: ASDF, ENTER, NUM-123
            [
                {t:'Caps',c:20,w:1.4,s:'mod'}, {t:'A',c:65}, {t:'S',c:83}, {t:'D',c:68}, {t:'F',c:70}, {t:'G',c:71}, {t:'H',c:72}, {t:'J',c:74}, {t:'K',c:75}, {t:'L',c:76}, {t:';',c:186}, {t:'\'',c:222}, {t:'ENTER',c:13,w:1.8,s:'ent'},
                {t:'1',c:97,s:'num'}, {t:'2',c:98,s:'num'}, {t:'3',c:99,s:'num'}, {t:'Ent',c:13,s:'num'}
            ],
            // ROW 5: ZXCV, SHIFT, UP, NUM-0.
            [
                {t:'Shift',c:160,w:1.8,m:1,s:'mod'}, {t:'Z',c:90}, {t:'X',c:88}, {t:'C',c:67}, {t:'V',c:86}, {t:'B',c:66}, {t:'N',c:78}, {t:'M',c:77}, {t:',',c:188}, {t:'.',c:190}, {t:'/',c:191}, {t:'↑',c:38,s:'arr'}, {t:'Shift',c:161,s:'mod',m:1},
                {t:'0',c:96,w:2,s:'num'}, {t:'.',c:110,s:'num'}, {t:'Ent',c:13,s:'num'}
            ],
            // ROW 6: CTRL, ALT, SPACE, ARROWS
            [
                {t:'Ctrl',c:162,w:1.2,m:1,s:'mod'}, {t:'Win',c:91,s:'mod'}, {t:'Alt',c:164,m:1,s:'mod'}, {t:'',c:32,s:'spc'}, {t:'Alt',c:165,m:1,s:'mod'}, {t:'Ctrl',c:163,m:1,s:'mod'}, {t:'←',c:37,s:'arr'}, {t:'↓',c:40,s:'arr'}, {t:'→',c:39,s:'arr'}, 
                {t:'0',c:96,w:2,s:'num'}, {t:'.',c:110,s:'num'}, {t:'Ent',c:13,s:'num'}
            ]
        ];

        // Фикс для раскладки: удаляем дубли кнопок из массива визуализации, если они перекрываются
        // Но в данном случае я специально сшил Numpad справа.
        // Чтобы 'Ent' нампада не дублировался визуально в строках 4, 5, 6 (он обычно большой),
        // я сделал его узким в каждой строке, чтобы сохранить сетку.
        // Numpad '+' тоже часто занимает 2 строки, тут разбит на две кнопки с одним кодом.

        const kb = document.getElementById('kb');
        const ip = localStorage.getItem('xrd_ip');

        L.forEach(row => {
            const r = document.createElement('div');
            r.className = 'row';
            row.forEach(k => {
                const b = document.createElement('div');
                b.className = 'k ' + (k.s||'');
                b.innerText = k.t;
                if(k.w) b.style.flex = k.w;
                
                const on = (e) => {
                    e.preventDefault();
                    if(k.m) { // toggle mode (shift/ctrl)
                        let a = b.classList.toggle('on');
                        send(k.c, a);
                    } else {
                        if(b.classList.contains('on')) return;
                        b.classList.add('on');
                        send(k.c, true);
                    }
                };
                const off = (e) => {
                    e.preventDefault();
                    if(!k.m) {
                        b.classList.remove('on');
                        send(k.c, false);
                    }
                };

                b.addEventListener('touchstart', on, {passive:false});
                b.addEventListener('touchend', off, {passive:false});
                // защита от промаха пальцем
                b.addEventListener('touchmove', e => {
                   let el = document.elementFromPoint(e.touches[0].clientX, e.touches[0].clientY);
                   if(el !== b && !k.m) off(e); 
                }, {passive:false});

                r.appendChild(b);
            });
            kb.appendChild(r);
        });

        function send(c, s) {
            if(!ip || !c) return;
            fetch(`http://${ip}/command`, {method:'POST', body:JSON.stringify({cmd:'key_event', id:c, vol:s?1:0})}).catch(()=>{});
        }
    </script>
</body>
</html>";
        }
    }
}