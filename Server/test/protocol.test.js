import assert from 'node:assert/strict';
import test from 'node:test';

import {
    PROTOCOL_VERSION,
    ProtocolError,
    createPlayerJoinedMessage,
    createRoomSnapshotMessage,
    encodeMessage,
    parseClientMessage
} from '../src/protocol.js';

test('encodeMessage 会为服务端消息自动补齐协议版本字段', () =>
{
    const encoded = encodeMessage(createRoomSnapshotMessage({
        roomCode: 'ABCDEF',
        players: [
            {
                playerId: 'player-1',
                playerName: '主机',
                state: null
            }
        ]
    }));
    const parsed = JSON.parse(encoded);

    assert.equal(parsed.v, PROTOCOL_VERSION);
    assert.equal(parsed.type, 'room_snapshot');
    assert.equal(parsed.roomCode, 'ABCDEF');
    assert.deepEqual(parsed.players, [
        {
            playerId: 'player-1',
            playerName: '主机',
            state: null
        }
    ]);
});

test('parseClientMessage 会把旧版 sync_state 兼容为 player_state_update', () =>
{
    const message = parseClientMessage(JSON.stringify({
        v: PROTOCOL_VERSION,
        type: 'sync_state',
        roomCode: 'ABCDEF',
        data: {
            pomodoro: {
                phase: 0,
                remainingSeconds: 1200,
                currentRound: 1,
                totalRounds: 4,
                isRunning: false
            },
            activeApp: null
        }
    }));

    assert.equal(message.type, 'player_state_update');
    assert.equal(message.roomCode, 'ABCDEF');
    assert.equal(message.state.pomodoro.remainingSeconds, 1200);
});

test('parseClientMessage 在协议版本不匹配时抛出 INVALID_VERSION', () =>
{
    assert.throws(
        () => parseClientMessage(JSON.stringify({
            v: PROTOCOL_VERSION + 1,
            type: 'create_room',
            playerName: '版本错误'
        })),
        (error) => error instanceof ProtocolError && error.code === 'INVALID_VERSION'
    );
});

test('player_joined 使用 players 数组携带远端玩家资料，避免新增字段', () =>
{
    const encoded = encodeMessage(createPlayerJoinedMessage({
        roomCode: 'ABCDEF',
        player: {
            playerId: 'player-2',
            playerName: '加入者',
            state: null
        }
    }));
    const parsed = JSON.parse(encoded);

    assert.deepEqual(parsed, {
        v: PROTOCOL_VERSION,
        type: 'player_joined',
        roomCode: 'ABCDEF',
        players: [
            {
                playerId: 'player-2',
                playerName: '加入者',
                state: null
            }
        ]
    });
});
