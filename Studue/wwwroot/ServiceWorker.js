self.addEventListener("push", event => {
    const data = event.data?.json() ?? { title: "Hello", body: "No payload" };

    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            data: { url: data.url }
        })
    );
});

self.addEventListener("notificationclick", event => {
    event.notification.close();

    const url = event.notification.data?.url;
    if (!url) return;

    // focus the tab if it is already open on that assignment, otherwise open one
    event.waitUntil(
        self.clients.matchAll({ type: "window", includeUncontrolled: true }).then(clients => {
            for (const client of clients) {
                if (client.url === url && "focus" in client) return client.focus();
            }
            return self.clients.openWindow(url);
        })
    );
});
