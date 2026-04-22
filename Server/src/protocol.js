import { RoomManagerError } from './RoomManager.js';

export const PROTOCOL_VERSION = 1;

const SUPPORTED_CLIENT_MESSAGE_TYPES = new Set([
    'create_room',
    'join_room',
    'leave_room',
    'player_state_update',
    'sync_state',
    'icon_upload',
    'icon_request',
    'ping',
    'pong'
]);

export class ProtocolError extends Error
{
    constructor(code, message)
    {
        super(message);
        this.code = code;
    }
}

export function parseClientMessage(rawMessage)
{
    let parsedMessage;

    try
    {
        parsedMessage = JSON.parse(rawMessage);
    }
    catch (error)
    {
        throw new ProtocolError('INVALID_JSON', '消息不是合法 JSON');
    }

    if (!parsedMessage || typeof parsedMessage !== 'object')
    {
        throw new ProtocolError('INVALID_MESSAGE', '消息体必须是对象');
    }

    if (parsedMessage.v !== PROTOCOL_VERSION)
    {
        throw new ProtocolError('INVALID_VERSION', '协议版本不匹配');
    }

    if (!SUPPORTED_CLIENT_MESSAGE_TYPES.has(parsedMessage.type))
    {
        throw new ProtocolError('UNSUPPORTED_MESSAGE', '不支持的消息类型');
    }

    switch (parsedMessage.type)
    {
        case 'create_room':
            return {
                v: PROTOCOL_VERSION,
                type: 'create_room',
                playerName: normalizePlayerName(parsedMessage.playerName),
                roomCode: normalizeOptionalRoomCode(parsedMessage.roomCode ?? parsedMessage.code)
            };

        case 'join_room':
            return {
                v: PROTOCOL_VERSION,
                type: 'join_room',
                roomCode: normalizeRequiredRoomCode(parsedMessage.roomCode ?? parsedMessage.code),
                playerName: normalizePlayerName(parsedMessage.playerName)
            };

        case 'leave_room':
            return {
                v: PROTOCOL_VERSION,
                type: 'leave_room'
            };

        case 'sync_state':
        case 'player_state_update':
            return {
                v: PROTOCOL_VERSION,
                type: 'player_state_update',
                roomCode: normalizeOptionalRoomCode(parsedMessage.roomCode ?? parsedMessage.code),
                state: normalizeRemoteState(parsedMessage.state ?? parsedMessage.data)
            };

        case 'icon_upload':
            return {
                v: PROTOCOL_VERSION,
                type: 'icon_upload',
                bundleId: normalizeBundleId(parsedMessage.bundleId),
                iconBase64: normalizeIconBase64(parsedMessage.iconBase64)
            };

        case 'icon_request':
            return {
                v: PROTOCOL_VERSION,
                type: 'icon_request',
                bundleIds: normalizeBundleIdArray(parsedMessage.bundleIds)
            };

        case 'ping':
            return {
                v: PROTOCOL_VERSION,
                type: 'ping'
            };

        case 'pong':
            return {
                v: PROTOCOL_VERSION,
                type: 'pong'
            };

        default:
            throw new ProtocolError('UNSUPPORTED_MESSAGE', '不支持的消息类型');
    }
}

export function encodeMessage(message)
{
    return JSON.stringify({
        v: PROTOCOL_VERSION,
        ...stripUndefinedFields(message)
    });
}

export function createRoomCreatedMessage({ roomCode, playerId })
{
    return {
        type: 'room_created',
        roomCode,
        playerId
    };
}

export function createRoomJoinedMessage({ roomCode, playerId })
{
    return {
        type: 'room_joined',
        roomCode,
        playerId
    };
}

export function createRoomSnapshotMessage({ roomCode, players })
{
    return {
        type: 'room_snapshot',
        roomCode,
        players: normalizePlayers(players)
    };
}

export function createPlayerJoinedMessage({ roomCode, player })
{
    return {
        type: 'player_joined',
        roomCode,
        players: normalizePlayers([player])
    };
}

export function createPlayerLeftMessage({ roomCode, playerId })
{
    return {
        type: 'player_left',
        roomCode,
        playerId
    };
}

export function createPlayerStateBroadcastMessage({ roomCode, playerId, state })
{
    return {
        type: 'player_state_broadcast',
        roomCode,
        playerId,
        state: normalizeRemoteState(state)
    };
}

export function createErrorMessage(error)
{
    return {
        type: 'error',
        error: normalizeErrorCode(error)
    };
}

export function createIconNeedMessage({ bundleId })
{
    return { type: 'icon_need', bundleId };
}

export function createIconBroadcastMessage({ bundleId, iconBase64 })
{
    return { type: 'icon_broadcast', bundleId, iconBase64 };
}

function normalizePlayerName(playerName)
{
    if (typeof playerName !== 'string' || !playerName.trim())
    {
        throw new ProtocolError('INVALID_MESSAGE', 'playerName 不能为空');
    }

    return playerName.trim();
}

function normalizeRequiredRoomCode(roomCode)
{
    const normalizedRoomCode = normalizeOptionalRoomCode(roomCode);
    if (!normalizedRoomCode)
    {
        throw new ProtocolError('INVALID_MESSAGE', 'roomCode 不能为空');
    }

    return normalizedRoomCode;
}

function normalizeOptionalRoomCode(roomCode)
{
    return typeof roomCode === 'string'
        ? roomCode.trim().toUpperCase()
        : '';
}

function normalizeRemoteState(state)
{
    try
    {
        return {
            pomodoro: {
                phase: normalizeInteger(state?.pomodoro?.phase),
                remainingSeconds: normalizeInteger(state?.pomodoro?.remainingSeconds),
                currentRound: normalizeInteger(state?.pomodoro?.currentRound),
                totalRounds: normalizeInteger(state?.pomodoro?.totalRounds),
                isRunning: Boolean(state?.pomodoro?.isRunning)
            },
            activeApp: state?.activeApp ?? null
        };
    }
    catch (error)
    {
        throw new ProtocolError('INVALID_MESSAGE', 'state 字段不合法');
    }
}

function normalizePlayers(players)
{
    if (!Array.isArray(players))
    {
        throw new ProtocolError('INVALID_MESSAGE', 'players 必须是数组');
    }

    return players.map((player) => ({
        playerId: player.playerId,
        playerName: player.playerName,
        state: player.state == null ? null : normalizeRemoteState(player.state)
    }));
}

function normalizeInteger(value)
{
    if (!Number.isInteger(value))
    {
        throw new Error('整数校验失败');
    }

    return value;
}

function normalizeErrorCode(error)
{
    if (typeof error === 'string' && error)
    {
        return error;
    }

    if (error instanceof ProtocolError || error instanceof RoomManagerError)
    {
        return error.code;
    }

    return 'INTERNAL_ERROR';
}

function stripUndefinedFields(message)
{
    return Object.fromEntries(
        Object.entries(message).filter(([, value]) => value !== undefined)
    );
}

function normalizeBundleId(bundleId)
{
    if (typeof bundleId !== 'string' || !bundleId.trim())
    {
        throw new ProtocolError('INVALID_MESSAGE', 'bundleId 不能为空');
    }
    return bundleId.trim();
}

function normalizeIconBase64(iconBase64)
{
    if (typeof iconBase64 !== 'string' || !iconBase64.trim())
    {
        throw new ProtocolError('INVALID_MESSAGE', 'iconBase64 不能为空');
    }
    return iconBase64;
}

function normalizeBundleIdArray(bundleIds)
{
    if (!Array.isArray(bundleIds) || bundleIds.length === 0)
    {
        throw new ProtocolError('INVALID_MESSAGE', 'bundleIds 必须是非空数组');
    }
    return bundleIds.map((bundleId) => normalizeBundleId(bundleId));
}
