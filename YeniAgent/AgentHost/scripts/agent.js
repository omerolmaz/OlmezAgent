const handlers = {};

handlers[''webrtcsdp''] = command => ({
    handled: true,
    success: true,
    payload: {
        ack: true,
        type: ''sdp'',
        sessionId: command.sessionid,
        data: command.data
    }
});

handlers[''webrtcice''] = command => ({
    handled: true,
    success: true,
    payload: {
        ack: true,
        type: ''ice'',
        sessionId: command.sessionid,
        data: command.data
    }
});

handlers[''chat''] = command => ({
    handled: true,
    success: true,
    payload: {
        mirrored: true,
        timestamp: new Date().toISOString(),
        sessionId: command.sessionid,
        message: command.data?.message ?? ''
    }
});

const bridge = {
    canHandle(action) {
        return !!handlers[action];
    },
    handle(action, commandJson) {
        const handler = handlers[action];
        if (!handler) {
            return null;
        }
        const command = JSON.parse(commandJson);
        const result = handler(command);
        if (!result) {
            return null;
        }
        if (result.handled === undefined) {
            result.handled = true;
        }
        return JSON.stringify(result);
    },
    list() {
        return Object.keys(handlers);
    }
};

globalThis.bridge = bridge;