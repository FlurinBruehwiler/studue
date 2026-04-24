self.addEventListener("push", event => {
    const data = event.data?.json() ?? { title: "Hello", body: "No payload" };

    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body
        })
    );
});